using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SpaceEngineersMap
{
    public class EnvironmentItem
    {
        public List<byte> Biomes { get; set; }
        public List<string> Materials { get; set; }
        public Rule Rule { get; set; }

        public static EnvironmentItem FromXML(XElement el)
        {
            var biomes = el.Element("Biomes");
            var mats = el.Element("Materials");
            var rule = el.Element("Rule");

            return new EnvironmentItem
            {
                Biomes = biomes?.Elements("Biome")?.Select(e => byte.Parse(e.Value))?.ToList(),
                Materials = mats?.Elements("Material")?.Select(e => e.Value)?.ToList(),
                Rule = Rule.FromXML(rule)
            };
        }


        public static Dictionary<string, List<EnvironmentItem>> LoadMaterialGroups(string planetdefsfile)
        {
            var xdoc = XDocument.Load(planetdefsfile);

            return
                xdoc.Root
                    .Elements("Definition")
                    .Single(d => d.Element("Id").Element("SubtypeId").Value == "EarthLike")
                    .Element("EnvironmentItems")
                    .Elements("Item")
                    .SelectMany(e => e.Element("Biomes").Elements("Biome").Select(b => new { key = b.Value, val = e }))
                    .GroupBy(a => a.key)
                    .ToDictionary(g => g.Key, g => g.Select(a => EnvironmentItem.FromXML(a.val)).ToList());
        }

    }
}
