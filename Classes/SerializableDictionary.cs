﻿
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MarvinsAIRARefactored.Classes;

[Serializable]
[XmlRoot( "dictionary" )]
public class SerializableDictionary<TKey, TValue> : SortedDictionary<TKey, TValue>, IXmlSerializable where TKey : notnull
{
	public XmlSchema? GetSchema()
	{
		return null;
	}

	public void ReadXml( XmlReader reader )
	{
		var keySerializer = new XmlSerializer( typeof( TKey ) );
		var valueSerializer = new XmlSerializer( typeof( TValue ) );

		bool wasEmpty = reader.IsEmptyElement;

		reader.Read();

		if ( wasEmpty )
		{
			return;
		}

		while ( reader.NodeType != XmlNodeType.EndElement )
		{
			reader.ReadStartElement( "item" );
			reader.ReadStartElement( "key" );

			TKey? key = (TKey?) keySerializer.Deserialize( reader );

			reader.ReadEndElement();
			reader.ReadStartElement( "value" );

			TValue? value = (TValue?) valueSerializer.Deserialize( reader );

			reader.ReadEndElement();

			if ( ( key != null ) && ( value != null ) )
			{
				Add( key, value );
			}

			reader.ReadEndElement();
			reader.MoveToContent();
		}

		reader.ReadEndElement();
	}

	public void WriteXml( XmlWriter writer )
	{
		var keySerializer = new XmlSerializer( typeof( TKey ) );
		var valueSerializer = new XmlSerializer( typeof( TValue ) );

		foreach ( TKey key in this.Keys )
		{
			writer.WriteStartElement( "item" );
			writer.WriteStartElement( "key" );

			keySerializer.Serialize( writer, key );

			writer.WriteEndElement();
			writer.WriteStartElement( "value" );

			valueSerializer.Serialize( writer, this[ key ] );

			writer.WriteEndElement();
			writer.WriteEndElement();
		}
	}
}
