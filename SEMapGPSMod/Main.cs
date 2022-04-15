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
        private readonly Regex NameRegex = new Regex(@"^P\d\d.\d\d.\d\d.\d\d[$%#@]*");
        private readonly Regex AddSecondRegex = new Regex(@"^[+]\d\d[$%#@]*");
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
            var args = messageText.Split(new[] { ' ' }, 2);

            if (args[0] == "/semapgps" && args.Length == 2)
            {
                AddSEMapGPS(args[1]);
                sendToOthers = false;
            }
        }

        private void AddSEMapGPS(long identityid, string name, string desc, bool replace)
        {
            var pos = MyAPIGateway.Session.Player.GetPosition();
            VRage.Game.ModAPI.IMyGps gps = null;

            if (replace)
            {
                var gpslist = MyAPIGateway.Session.GPS.GetGpsList(identityid);
                gps = gpslist.FirstOrDefault(e => e.Name == name && e.Description == desc);
            }

            if (gps != null)
            {
                gps.Coords = pos;
                MyAPIGateway.Session.GPS.ModifyGps(identityid, gps);
                MyAPIGateway.Utilities.ShowNotification($"Updated GPS [{name}]");
                MyAPIGateway.Utilities.ShowMessage("SEMapGPS", $"Updated GPS [{name}]");
            }
            else
            {
                gps = MyAPIGateway.Session.GPS.Create(name, desc, pos, false);
                MyAPIGateway.Session.GPS.AddGps(identityid, gps);
                MyAPIGateway.Utilities.ShowNotification($"Created GPS [{name}]");
                MyAPIGateway.Utilities.ShowMessage("SEMapGPS", $"Created GPS [{name}]");
            }

            MyAPIGateway.Session.Save();
        }

        private void RemoveSEMapGPS(long identityid, string name, string desc)
        {
            var gpslist = MyAPIGateway.Session.GPS.GetGpsList(identityid);
            var gps = gpslist.FirstOrDefault(e => e.Name == name && e.Description == desc);

            if (gps != null)
            {
                MyAPIGateway.Session.GPS.RemoveGps(identityid, gps);
                MyAPIGateway.Utilities.ShowNotification($"Deleted GPS [{name}]");
                MyAPIGateway.Utilities.ShowMessage("SEMapGPS", $"Deleted GPS [{name}]");
            }
        }

        private void AddSEMapGPS(string argstr)
        {
            var identid = MyAPIGateway.Session.Player.IdentityId;
            bool replace = false;
            bool delete = false;

            if (argstr.StartsWith("replace "))
            {
                replace = true;
                argstr = argstr.Substring(8);
            }
            else if (argstr.StartsWith("delete "))
            {
                delete = true;
                argstr = argstr.Substring(7);
            }

            if (argstr.StartsWith("[") && argstr.Contains("] "))
            {
                var argsplit = argstr.Split(new[] { ']' }, 2);
                var identname = argsplit[0].TrimStart('[').TrimEnd(']');
                argstr = argsplit[1].TrimStart(' ');

                var idents = new List<VRage.Game.ModAPI.IMyIdentity>();
                MyAPIGateway.Players.GetAllIdentites(idents);
                var ident = idents.FirstOrDefault(e => string.Equals(identname, e.DisplayName));

                if (ident != null)
                {
                    identid = ident.IdentityId;
                }
                else
                {
                    MyAPIGateway.Utilities.ShowNotification($"Cannot find identity [{identname}]");
                }
            }

            var args = argstr.Split(new[] { ' ' }, 2);
            var name = args[0];
            var desc = args.Length == 2 ? args[1] : "";

            if (delete)
            {
                RemoveSEMapGPS(identid, name, desc);
            }
            else if (NameRegex.IsMatch(name))
            {
                var parts = name.Split('.');
                string timestr = $"{parts[1]}:{parts[2]}:{parts[3].Substring(0, 2)}";
                TimeSpan time;

                if (TimeSpan.TryParse(timestr, CultureInfo.InvariantCulture, out time))
                {
                    Part = parts[0];
                    PartTime = time;
                    AddSEMapGPS(identid, name, desc, replace);
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
                AddSEMapGPS(identid, name, desc, replace);
            }
            else
            {
                MyAPIGateway.Utilities.ShowNotification($"Unrecognised name [{name}]");
            }
        }
    }
}
