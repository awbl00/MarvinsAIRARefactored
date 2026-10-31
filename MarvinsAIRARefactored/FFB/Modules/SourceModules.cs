
using MarvinsAIRARefactored.Classes;

namespace MarvinsAIRARefactored.FFB.Modules;

/// <summary>
/// 60 Hz source. Emits the 60 Hz torque sample (Nm). Prediction is no longer part of this source — it is a
/// separate Prediction module placed in the graph (see <see cref="PredictionModule"/>), so this just surfaces
/// the raw sample.
/// </summary>
public sealed class Source60HzModule : FFBModule
{
	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		return ctx.Torque60Hz;
	}
}

/// <summary>360 Hz source. Emits the raw 360 Hz torque sample (Nm).</summary>
public sealed class Source360HzModule : FFBModule
{
	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		return ctx.Torque360Hz;
	}
}

/// <summary>
/// LFE source. Emits the low-frequency-effects audio magnitude scaled to full wheel force (Nm) and the module's
/// Strength, gated on being on track (old 1413–1416). Sum it into the main signal with a Mixer; Strength sets its
/// level (a downstream Gain, if any, still composes). Strength defaults to 25%.
/// </summary>
public sealed class SourceLFEModule : FFBModule
{
	private const int Strength = 1;

	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		return ctx.IsOnTrack ? ctx.LFEMagnitude * ctx.MaxForce * _v[ Strength ] : 0f;
	}
}

/// <summary>
/// Wheel velocity source: how fast the steering wheel is being turned, from the sim's 60 Hz SteeringWheelAngle
/// telemetry (radians — so it is independent of the wheel's rotation-range setting), scaled so one full wheel
/// revolution per second equals full wheel force (Nm) times the module's Strength, and gated on being on track.
/// The sign opposes the turn direction, so summing it into the main signal produces friction/damping: Strength
/// sets the damping level (a downstream Gain still composes), and a low-pass ahead of the Add smooths the 60 Hz
/// staircase. Replaces the old fixed-function Friction module. Strength defaults to 100% (pass-through).
/// </summary>
public sealed class SourceWheelVelocityModule : FFBModule
{
	private const int Strength = 1;

	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		// negated: iRacing's angle is positive counterclockwise, while the output convention needs the
		// opposite sign to RESIST wheel motion (matching the old DirectInput-axis-derived friction)
		return ctx.IsOnTrack ? -ctx.SteeringWheelVelocity / MathF.Tau * ctx.MaxForce * _v[ Strength ] : 0f;
	}
}

/// <summary>
/// Friction source: dry friction via a dragged stick point — the same model wheel firmwares use for their
/// hardware friction effect. The module latches the wheel angle it is "stuck" at and pulls back toward it
/// like a spring, saturating at the Strength setting (stored in physical Nm at the wheel — the breakaway /
/// sliding friction torque); when the wheel is turned more than StickRegion
/// degrees from the stick point, the point is dragged along at that distance. Turning steadily therefore
/// feels like constant sliding friction, and at rest the wheel is held planted at the stick point. Because
/// the model is driven by the wheel's POSITION (no differentiation, no velocity noise) and its torque is
/// bounded by the breakaway level, it does not need a velocity signal the way the old velocity-based
/// friction did. A larger StickRegion feels softer/springier before breakaway; a smaller one feels crisper.
/// Sum it into the main signal with an Add; route it through a SpeedGain to fade it with car speed (parked
/// steering weight = full at rest, fading out by ~10 m/s).
/// <para>The spring acts on a LEAD-COMPENSATED angle: the telemetry angle is ~2 frames stale, and a stiff
/// spring on a stale angle behaves as negative damping — it pumps the wheel into a permanent oscillation
/// whose amplitude tracks the stick region (the drag boundary is the only place energy dissipates, so the
/// limit cycle parks right at it). Extrapolating the angle forward by the loop delay with the telemetry
/// velocity cancels that phase lag, which is what the wheel firmware gets for free by running the same model
/// at the servo rate with no delay. The centering source never needed this because its spring is 10-25×
/// softer (full force over a quarter of the steering range), putting its resonance where the delay phase is
/// negligible.</para>
/// </summary>
public sealed class SourceFrictionModule : FFBModule
{
	private const int Strength = 1;
	private const int StickRegion = 2;

	// how far ahead the spring's angle is extrapolated — sized to the telemetry-to-wheel loop delay (~30 ms)
	// plus a little extra, so the slight overcompensation contributes positive damping instead of none
	private const float LeadSeconds = 0.05f;

	private float _stickRegionRad = 1f;
	private float _stickPoint = 0f;
	private bool _stickPointValid = false;

	public override void Reset()
	{
		_stickPointValid = false;
	}

	protected override void OnValuesChanged()
	{
		// the knob is in degrees of wheel rotation; the telemetry angle is radians
		_stickRegionRad = MathF.Max( _v[ StickRegion ], 0.5f ) * ( MathF.PI / 180f );
	}

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		if ( !ctx.IsOnTrack )
		{
			_stickPointValid = false;

			return 0f;
		}

		if ( !_stickPointValid )
		{
			_stickPoint = ctx.SteeringWheelAngle;
			_stickPointValid = true;
		}

		// the stick point is dragged along using the MEASURED angle, so it stays anchored to real positions;
		// past the breakaway distance it trails the wheel, pinning the torque at the sliding-friction level
		var displacement = ctx.SteeringWheelAngle - _stickPoint;

		if ( displacement > _stickRegionRad )
		{
			_stickPoint = ctx.SteeringWheelAngle - _stickRegionRad;
		}
		else if ( displacement < -_stickRegionRad )
		{
			_stickPoint = ctx.SteeringWheelAngle + _stickRegionRad;
		}

		// the spring acts on the lead-compensated angle (see the class comment — this is what keeps the stick
		// phase from pumping a permanent oscillation)
		var leadDisplacement = ctx.SteeringWheelAngle + ctx.SteeringWheelVelocity * LeadSeconds - _stickPoint;

		// negated: the angle is positive counterclockwise (like the output convention), and the spring must
		// pull the wheel BACK toward the stick point. Strength is physical Nm at the wheel; via WheelForce it
		// becomes a normalized fraction, then MaxForce lifts it into the Nm main chain so the physical friction
		// level survives the output normalization regardless of the max force setting
		return -Math.Clamp( leadDisplacement / _stickRegionRad, -1f, 1f ) * ( _v[ Strength ] / ctx.WheelForce ) * ctx.MaxForce;
	}
}

/// <summary>
/// Dynamic damping source: viscous damping (torque opposing wheel speed, proportional to it) whose strength
/// depends on what the wheel is doing. Input A is the main torque signal, used only to classify the situation;
/// the module emits ONLY the damping torque, so sum it back into the main signal with an Add. It is a side-chain
/// source: never spliced into the selected wire when added, and silent (not pass-through) when disabled.
/// <para>Velocity comes from an anchor that chases the wheel angle (the friction source's stick point, but
/// dragged exponentially instead of at a fixed distance): the anchor trails the wheel by an amount proportional
/// to wheel speed, so the lag is a smoothed velocity without differentiating the 60 Hz angle. The anchor is
/// stepped once per 60 Hz frame (the angle only changes per frame — stepping it at 360 Hz would ripple the
/// velocity at 60 Hz), with a scale that makes the steady-state estimate exact.</para>
/// <para>Four weights cover the four situations, by whether the torque pushes WITH the wheel's motion (the FFB
/// is driving the wheel) or AGAINST it, and whether the wheel is speeding up or slowing down:
/// accelerating with torque (snap-back / hands off — strongest), decelerating with torque (the driver catching
/// it), accelerating against torque (the driver turning in — light), decelerating against torque (the FFB
/// already braking the wheel — little or none). The with/against split is blended across a small torque band
/// and the accelerating/decelerating split across an acceleration threshold, so no situation change steps the
/// output; the combined weight then follows an attack/release envelope.</para>
/// <para>Strength caps the damping torque (physical Nm at the wheel, like the friction source); Coefficient is
/// the damping torque at one wheel revolution per second. The telemetry is ~2 frames stale, so very high
/// coefficients can add energy above a few Hz instead of removing it — tune on a hands-off oscillation.</para>
/// <para>Settings from VelocitySmoothing onward are TEMPORARY tuning knobs, to be replaced by constants once
/// their values are finalized.</para>
/// </summary>
public sealed class SourceDynamicDampingModule : FFBModule
{
	private const int Strength = 1;
	private const int Coefficient = 2;
	private const int AcceleratingWithTorque = 3;
	private const int DeceleratingWithTorque = 4;
	private const int AcceleratingAgainstTorque = 5;
	private const int DeceleratingAgainstTorque = 6;

	// temporary tuning knobs
	private const int VelocitySmoothing = 7;        // ms — anchor time constant
	private const int OutputSmoothing = 8;          // ms — smooths the 60 Hz steps out of the damping torque
	private const int TorqueSmoothing = 9;          // ms — smoothing on input A before classification
	private const int TorqueBand = 10;              // Nm — with/against blend width
	private const int AccelerationSmoothing = 11;   // ms
	private const int AccelerationThreshold = 12;   // deg/s² — speeding up by this much counts as fully accelerating
	private const int Attack = 13;                  // ms — weight rising
	private const int Release = 14;                 // ms — weight falling

	private const float TickSeconds = FFBTickContext.TickDeltaMilliseconds / 1000f;
	private const float FrameSeconds = TickSeconds * FFBTickContext.SamplesPerFrame;

	// cached derivations of the knob values (see OnValuesChanged)
	private float _anchorFollow = 1f;
	private float _velocityScale = 0f;
	private float _accelerationAlpha = 1f;
	private float _accelerationThresholdRad = 0f;
	private float _torqueAlpha = 1f;
	private float _outputAlpha = 1f;
	private float _attackAlpha = 1f;
	private float _releaseAlpha = 1f;

	private bool _stateValid = false;
	private float _anchor = 0f;
	private float _velocity = 0f;
	private float _acceleration = 0f;
	private float _torque = 0f;
	private float _weight = 0f;
	private float _output = 0f;

	public override void Reset()
	{
		_stateValid = false;
	}

	protected override void OnValuesChanged()
	{
		// the anchor keeps this fraction of its lag each frame; the velocity scale turns the post-step lag back
		// into exact wheel speed for a steady turn (lag = Δθ·r/(1−r), so velocity = lag·(1−r)/(r·dt))
		var anchorRetain = MathF.Exp( -FrameSeconds / ( MathF.Max( _v[ VelocitySmoothing ], 1f ) / 1000f ) );

		_anchorFollow = 1f - anchorRetain;
		_velocityScale = ( 1f - anchorRetain ) / ( anchorRetain * FrameSeconds );

		_accelerationAlpha = OnePoleAlpha( _v[ AccelerationSmoothing ], FrameSeconds );
		_accelerationThresholdRad = _v[ AccelerationThreshold ] * ( MathF.PI / 180f );
		_torqueAlpha = OnePoleAlpha( _v[ TorqueSmoothing ], TickSeconds );
		_outputAlpha = OnePoleAlpha( _v[ OutputSmoothing ], TickSeconds );
		_attackAlpha = OnePoleAlpha( _v[ Attack ], TickSeconds );
		_releaseAlpha = OnePoleAlpha( _v[ Release ], TickSeconds );
	}

	private static float OnePoleAlpha( float timeConstantMilliseconds, float stepSeconds )
	{
		return ( timeConstantMilliseconds <= 0f ) ? 1f : 1f - MathF.Exp( -stepSeconds / ( timeConstantMilliseconds / 1000f ) );
	}

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		if ( !ctx.IsOnTrack )
		{
			_stateValid = false;

			return 0f;
		}

		if ( !_stateValid )
		{
			_anchor = ctx.SteeringWheelAngle;
			_velocity = 0f;
			_acceleration = 0f;
			_torque = inputA;
			_weight = 0f;
			_output = 0f;
			_stateValid = true;
		}
		else if ( ctx.SampleIndex == 0 )
		{
			// new telemetry frame: step the anchor toward the new angle, read the speed off its lag, and take the
			// frame-to-frame change of that (already smoothed) speed as the acceleration
			_anchor += ( ctx.SteeringWheelAngle - _anchor ) * _anchorFollow;

			var previousVelocity = _velocity;

			_velocity = ( ctx.SteeringWheelAngle - _anchor ) * _velocityScale;

			var frameAcceleration = ( _velocity - previousVelocity ) / FrameSeconds;

			_acceleration += ( frameAcceleration - _acceleration ) * _accelerationAlpha;
		}

		_torque += ( inputA - _torque ) * _torqueAlpha;

		var velocitySign = MathF.Sign( _velocity );

		// positive while the wheel is speeding up, negative while it is slowing down
		var speedingUp = _acceleration * velocitySign;

		var acceleratingFactor = ( _accelerationThresholdRad > 0f ) ? Math.Clamp( speedingUp / _accelerationThresholdRad, 0f, 1f ) : ( ( speedingUp > 0f ) ? 1f : 0f );

		// positive while the torque pushes the wheel the way it is already moving (the FFB is driving the wheel);
		// angle, velocity, and torque all share the counterclockwise-positive convention
		var alignment = _torque * velocitySign;

		var torqueBand = _v[ TorqueBand ];

		var withTorqueFactor = ( torqueBand > 0f ) ? Math.Clamp( 0.5f + alignment / ( 2f * torqueBand ), 0f, 1f ) : ( ( alignment > 0f ) ? 1f : 0f );

		var withTorqueWeight = MathZ.Lerp( _v[ DeceleratingWithTorque ], _v[ AcceleratingWithTorque ], acceleratingFactor );
		var againstTorqueWeight = MathZ.Lerp( _v[ DeceleratingAgainstTorque ], _v[ AcceleratingAgainstTorque ], acceleratingFactor );

		var targetWeight = MathZ.Lerp( againstTorqueWeight, withTorqueWeight, withTorqueFactor );

		_weight += ( targetWeight - _weight ) * ( ( targetWeight > _weight ) ? _attackAlpha : _releaseAlpha );

		// Coefficient is Nm at one revolution per second; Strength caps the result (both physical Nm at the wheel)
		var dampingTorqueNm = Math.Clamp( _velocity / MathF.Tau * _v[ Coefficient ] * _weight, -_v[ Strength ], _v[ Strength ] );

		// negated: damping must OPPOSE the counterclockwise-positive velocity. Physical Nm becomes a normalized
		// fraction via WheelForce, then MaxForce lifts it into the Nm main chain (same as the friction source)
		var targetOutput = -dampingTorqueNm / ctx.WheelForce * ctx.MaxForce;

		_output += ( targetOutput - _output ) * _outputAlpha;

		return _output;
	}
}

/// <summary>
/// Soft lock source (old 1420–1435): emits an opposing force once the steering wheel is turned past the car's
/// maximum steering angle, scaled by strength and MaxForce (Nm). Sum it into the main signal with an Add.
/// </summary>
public sealed class SourceSoftLockModule : FFBModule
{
	private const int Strength = 1;

	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		var strength = _v[ Strength ];

		if ( strength > 0f )
		{
			var deltaToMax = ( ctx.SteeringWheelAngleMax * 0.5f ) - MathF.Abs( ctx.SteeringWheelAngle );

			if ( deltaToMax < 0f )
			{
				var sign = MathF.Sign( ctx.SteeringWheelAngle );

				var contribution = sign * deltaToMax * 2f * strength;

				// -WheelVelocity: the old code damped with the DirectInput axis velocity, whose sign convention is
				// the opposite of the sim-telemetry-derived WheelVelocity (iRacing counterclockwise-positive)
				if ( MathF.Sign( -ctx.WheelVelocity ) != sign )
				{
					contribution += -ctx.WheelVelocity * strength;
				}

				return contribution * ctx.MaxForce;
			}
		}

		return 0f;
	}
}

/// <summary>
/// Wheel centering source (old 1453–1461): emits a centering force from the wheel's position and velocity,
/// scaled by strength and MaxForce (Nm), gated on being on track. Sum it into the main signal with an Add;
/// route it through a SpeedGain over the 0–5 mph ramp to gate it by speed (parked-only centering = GainAtMin 1
/// / GainAtMax 0, racing-only = the reverse — this replaced the old while-racing / while-parked toggles).
/// </summary>
public sealed class SourceWheelCenteringModule : FFBModule
{
	private const int Strength = 1;

	public override void Reset() { }

	public override float Process( in FFBTickContext ctx, float inputA, float inputB )
	{
		if ( ctx.IsOnTrack )
		{
			// negated: WheelPosition/Velocity follow iRacing's counterclockwise-positive convention (the old code
			// used the DirectInput axis, which is the opposite), while the centering torque must oppose the position
			return -Math.Clamp( ( Math.Clamp( ctx.WheelPosition, -0.25f, 0.25f ) + ctx.WheelVelocity * 0.1f ) * _v[ Strength ], -1f, 1f ) * ctx.MaxForce;
		}

		return 0f;
	}
}
