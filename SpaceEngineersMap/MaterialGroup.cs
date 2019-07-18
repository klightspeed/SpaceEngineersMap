using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class MaterialGroup
    {
        public string Name { get; set; }
        public byte Value { get; set; }
        public List<Rule> Rules { get; set; }

        public static MaterialGroup FromXML(XElement el)
        {
            return new MaterialGroup
            {
                Name = el.Attribute("Name").Value,
                Value = byte.Parse(el.Attribute("Value").Value),
                Rules = el.Elements("Rule").Select(e => Rule.FromXML(e)).ToList()
            };
        }

        public static Dictionary<string, List<MaterialGroup>> LoadMaterialGroups(string planetdefsfile)
        {
            var xdoc = XDocument.Load(planetdefsfile);

            return
                xdoc.Root
                    .Elements("Definition")
                    .Single(d => d.Element("Id").Element("SubtypeId").Value == "EarthLike")
                    .Element("ComplexMaterials")
                    .Elements("MaterialGroup")
                    .GroupBy(e => e.Attribute("Value").Value)
                    .ToDictionary(g => g.Key, g => g.Select(e => FromXML(e)).ToList());
        }
    }
}
