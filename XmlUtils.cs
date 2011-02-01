using System;
using System.Xml;

namespace FSClient {
	class XmlUtils {
		public static String GetNodeAttrib(XmlNode node, String name) {
			if (node.Attributes[name] == null)
				return "";
			return node.Attributes[name].Value;
		}
		public static String GetNodeValue(XmlNode node, String name) {
			if (node.SelectSingleNode(name) == null)
				return "";
			return node.SelectSingleNode(name).InnerText;
		}
		public static XmlDocument GetDocument(String xml) {
			XmlDocument doc = new XmlDocument();
			doc.LoadXml(xml);
			return doc;
		}
		public static XmlNode GetNode(XmlDocument doc, String name, int num) {
			XmlNodeList res = doc.GetElementsByTagName(name);
			if (res.Count <= num)
				return null;
			return res[num];
		}
		public static String GetValue(XmlDocument doc, String name, int num) {
			XmlNode node = GetNode(doc, name, num);
			if (node == null)
				return "";
			return node.InnerText;
		}
		public static void AddNodeAttrib(XmlNode node, String name, String value) {
			XmlAttribute attrib = node.OwnerDocument.CreateAttribute(name);
			attrib.Value = value;
			node.Attributes.Append(attrib);
		}
		public static XmlNode AddNodeNode(XmlNode node, String name) {
			XmlNode new_node = node.OwnerDocument.CreateElement(name);
			node.AppendChild(new_node);
			return new_node;
		}
	}
}
