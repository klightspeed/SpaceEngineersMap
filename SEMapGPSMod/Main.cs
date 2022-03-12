using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using VRage.Game.Components;
using System.Text.RegularExpressions;

namespace SEMapGPSMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Main : MySessionComponentBase
    {
        private string Part;
        private TimeSpan PartTime;
        private readonly Regex NameRegex = new Regex(@"P\d\d.\d\d.\d\d.\d\d[$%#@]*");
        private readonly Regex AddSecondRegex = new Regex(@"[+]\d\d[$%#@]*");
        private bool IsInitialized;

        protected override void UnloadData()
        {
            base.UnloadData();
            MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (!IsInitialized)
            {
                MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
                IsInitialized = true;
            }
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            var args = messageText.Split(' ');

            if (args[0] == "/semapgps" && args.Length >= 2)
            {
                AddSEMapGPS(args);
                sendToOthers = false;
            }
        }

        private void AddSEMapGPS(string name, string desc)
        {
            var pos = MyAPIGateway.Session.Player.GetPosition();
            var gps = MyAPIGateway.Session.GPS.Create(name, desc, pos, false);
            MyAPIGateway.Session.GPS.AddGps(MyAPIGateway.Session.Player.IdentityId, gps);
            MyAPIGateway.Utilities.ShowNotification($"Created GPS [{name}]");
            MyAPIGateway.Utilities.ShowMessage("SEMapGPS", $"Created GPS [{name}]");
            MyAPIGateway.Session.Save();
        }

        private void AddSEMapGPS(string[] args)
        {
            var name = args[1];
            var desc = args.Length >= 3 ? string.Join(" ", args.Skip(2)) : "";

            if (NameRegex.IsMatch(name))
            {
                var parts = name.Split('.');
                string timestr = $"{parts[1]}:{parts[2]}:{parts[3].Substring(0, 2)}";

                if (TimeSpan.TryParse(timestr, CultureInfo.InvariantCulture, out TimeSpan time))
                {
                    Part = parts[0];
                    PartTime = time;
                    AddSEMapGPS(name, desc);
                }
                else
                {
                    MyAPIGateway.Utilities.ShowNotification($"Unrecognised name [{name}]");
                }
            }
            else if (AddSecondRegex.IsMatch(name))
            {
                int seconds = int.Parse(name.Substring(1, 2));
                PartTime += new TimeSpan(0, 0, seconds);
                name = $"{Part}.{PartTime.Hours:00}.{PartTime.Minutes:00}.{PartTime.Seconds:00}{name.Substring(3)}";
                AddSEMapGPS(name, desc);
            }
            else
            {
                MyAPIGateway.Utilities.ShowNotification($"Unrecognised name [{name}]");
            }
        }
    }
}
