﻿using System;
using System.Collections.Generic;
using System.Xml;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Mission;
using Pathfinder.Util;

namespace Pathfinder.Mission
{
    public class Instance : ActiveMission
    {
        private Dictionary<string, Tuple<bool, object>> keyToObject = new Dictionary<string, Tuple<bool, object>>();
        private List<MissionGoalInstance> moddedGoals = new List<MissionGoalInstance>();

        public IInterface Interface
        {
            get; private set;
        }

        public string InterfaceId
        {
            get; private set;
        }

        public Hacknet.Computer MissionComputer
        {
            get; internal set;
        }

        public Hacknet.Computer AgentComputer
        {
            get; internal set;
        }

        internal Instance(IInterface inter,
                        string next = "NONE",
                        MailServer.EMailData email = default(MailServer.EMailData))
            : base(inter.DefaultGoals ?? new List<MisisonGoal>(), next ?? "NONE", email)
        {
            Interface = inter;
            goals.ForEach((goal) =>
            {
                var g = goal as MissionGoalInstance;
                if (g != null)
                    g.Mission = this;
                Interface.OnGoalAdd(this, goal);
            });
        }

        public static Instance CreateInstance(string id,
                                              Dictionary<string, string> objects,
                                             string next = null,
                                              MailServer.EMailData? mailData = null)
        {
            id = Utility.GetId(id);
            var inter = Handler.GetMissionById(id);
            if (inter == null)
                return null;
            var i = new Instance(inter,
                                 next ?? inter.InitialNextMission ?? "NONE",
                                 mailData.HasValue ? mailData.Value : inter.IntialEmailData)
            {
                InterfaceId = id
            };
            i.Interface.OnLoadInstance(i, objects);
            return i;
        }

        public object this[string key]
        {
            get
            {
                key = Utility.ConvertToValidXmlAttributeName(key);
                Tuple<bool, object> t;
                if (keyToObject.TryGetValue(key, out t))
                    return t.Item2;
                return null;
            }
            set
            {
                key = Utility.ConvertToValidXmlAttributeName(key);
                keyToObject[key] = new Tuple<bool, object>(false, value);
            }
        }

        public object GetInstanceData(string key)
        {
            return this[key];
        }

        public T GetInstanceData<T>(string key)
        {
            return (T)GetInstanceData(key);
        }

        public bool? IsInstanceSaveable(string key)
        {
            key = Utility.ConvertToValidXmlAttributeName(key);
            Tuple<bool, object> t;
            if (keyToObject.TryGetValue(key, out t))
                return t.Item1;
            return null;
        }

        public bool SetInstanceData(string key, object val, bool shouldSave = false)
        {
            key = Utility.ConvertToValidXmlAttributeName(key);
            var t = new Tuple<bool, object>(shouldSave, val);
            keyToObject[key] = t;
            return keyToObject[key] == t;
        }

        public void SetInstanceDataSaveable(string key, bool shouldSave)
        {
            key = Utility.ConvertToValidXmlAttributeName(key);
            Tuple<bool, object> t;
            if (keyToObject.TryGetValue(key, out t))
            {
                t = new Tuple<bool, object>(shouldSave, t.Item2);
                keyToObject[key] = t;
            }
        }

        public Instance AddGoal(MissionGoalInstance goal)
        {
            goal.Mission = this;
            Interface.OnGoalAdd(this, goal);
            moddedGoals.Add(goal);
            return this;
        }

        public Instance AddGoal(string id)
        {
            if(Handler.ContainsMission(id))
                AddGoal(MissionGoalInstance.CreateInstance(id));
            return this;
        }

        public override void ActivateSuppressedStartFunctionIfPresent()
        {
            Tuple<string, int> t;
            if ((t = Interface.OnStart(this)) != null)
                this.addStartFunction(t.Item2, t.Item1);
            base.ActivateSuppressedStartFunctionIfPresent();
        }

        public override void finish()
        {
            Tuple<string, int> t;
            if ((t = Interface.OnEnd(this)) != null)
                this.addEndFunction(t.Item2, t.Item1);

            Hacknet.OS.currentInstance.branchMissions.Clear();
            if (this.nextMission.StartsWith("Pathfinder:", StringComparison.Ordinal))
            {
                var id = nextMission.Substring(nextMission.IndexOf(':') + 1);
                Hacknet.OS.currentInstance.currentMission = CreateInstance(id, new Dictionary<string, string>());
                Hacknet.OS.currentInstance.currentMission?.sendEmail(Hacknet.OS.currentInstance);
            }
            else if (!this.nextMission.Equals("NONE"))
            {
                var str = "Content/Missions";
                if (Settings.IsInExtensionMode)
                    str = ExtensionLoader.ActiveExtensionInfo.FolderPath;
                ComputerLoader.loadMission(str + "/" + this.nextMission, false);
            }
            else
                Hacknet.OS.currentInstance.currentMission = null;

            Hacknet.OS.currentInstance.currentMission?.ActivateSuppressedStartFunctionIfPresent();

            if (this.endFunctionName != null)
                MissionFunctions.runCommand(this.endFunctionValue, this.endFunctionName);

            Hacknet.OS.currentInstance.saveGame();
            if (Hacknet.OS.currentInstance.multiplayer)
                Hacknet.OS.currentInstance.endMultiplayerMatch(true);
        }

        public override bool isComplete(List<string> additionalDetails = null)
        {
            additionalDetails = additionalDetails ?? new List<string>();
            var result = Interface.IsComplete(this, additionalDetails);
            if (result.HasValue)
                return result.Value;
            foreach (var g in moddedGoals)
                if (!g.isComplete(additionalDetails))
                    return false;
            return base.isComplete(additionalDetails);
        }

        public override void Update(float t)
        {
            Interface.Update(this, t);
            base.Update(t);
        }

        public override string getSaveString()
        {
            var goalStr = "";
            int i = 0;
            foreach (var g in moddedGoals)
                goalStr += (i > 0 ? "," : "") + g.InterfaceId;
            var str = "<moddedMission interfaceId=\"" + InterfaceId
                + "\" next=\"" + (nextMission.StartsWith("Pathfinder:", StringComparison.Ordinal) ? nextMission : "/"+nextMission)
                + "\" moddedGoals=" + goalStr + " storedObjects=\"";
            foreach (var o in keyToObject)
            {
                if (o.Value.Item1)
                    str += " " + o.Key + "|" + o.Value.Item2;
            }
            str += "\"";
            if (wasAutoGenerated)
                str += " genTarget=\"" + genTarget + "\" genFile=\"" + genFile
                    + "\" genPath=\"" + genPath + "\"  genTargetName=\""
                    + genTargetName + "\" genOther=\"" + genOther + "\"";

            str += "activeCheck=\"" + activeCheck + "\">\n";
            str += "<email sender=\"" + Folder.Filter(email.sender)
                                              + "\" subject=\"" + Folder.Filter(email.subject) + "\">"
                                              + Folder.Filter(email.body) + "</email>";
            str += "<endFunc val=\"" + endFunctionValue + "\" name=\"" + endFunctionName + "\" />";
            str += "<posting title=\"" + Folder.Filter(postingTitle) + "\" >" + Folder.Filter(postingBody) + "</posting>";
            str += "</moddedMission>";
            return str;
        }

        public override void sendEmail(Hacknet.OS os)
        {
            if (Interface.SendEmail(this, os))
                base.sendEmail(os);
        }

        public static Instance Load(XmlReader reader)
        {
            while (reader.Name != "moddedMission")
                reader.Read();
            reader.MoveToAttribute("interfaceId");
            var missionId = reader.ReadContentAsString();
            if (!Handler.ContainsMission(missionId)) return null;
            reader.MoveToAttribute("next");
            var next = reader.ReadContentAsString().Replace('\\', '/');
            reader.MoveToAttribute("moddedGoals");
            var moddedGoals = reader.ReadContentAsString().Split(',');
            reader.MoveToAttribute("storedObjects");
            var storedObjectStr = reader.ReadContentAsString();
            var storedObjects = new Dictionary<string, string>();
            if (storedObjectStr.Length != 0)
            {
                foreach (var o in storedObjectStr.Split(' '))
                {
                    var i = o.IndexOf('|');
                    storedObjects[o.Remove(i)] = o.Substring(i + 1);
                }
            }
            if (reader.MoveToAttribute("genTarget"))
            {
                MissionGenerationParser.Comp = reader.ReadContentAsString();
                reader.MoveToAttribute("genFile");
                MissionGenerationParser.File = reader.ReadContentAsString();
                reader.MoveToAttribute("genPath");
                MissionGenerationParser.Path = reader.ReadContentAsString();
                reader.MoveToAttribute("genTargetName");
                MissionGenerationParser.Target = reader.ReadContentAsString();
                reader.MoveToAttribute("genOther");
                MissionGenerationParser.Other = reader.ReadContentAsString();
            }
            reader.MoveToAttribute("activeCheck");
            var activeCheck = reader.ReadContentAsString().ToLower().Equals("true");
            Instance result = null;
            if (next != "NULL_MISSION")
            {
                var attachments = new List<string>();
                if (next.IndexOf('/') != -1)
                {
                    next = next.Substring(1);
                    if (!Settings.IsInExtensionMode && !next.StartsWith("Content", StringComparison.Ordinal))
                        next = "Content/" + next;
                    ActiveMission nextMission;
                    try
                    {
                        nextMission = ComputerLoader.readMission(next) as ActiveMission;
                        if (nextMission != null)
                        {
                            activeCheck = activeCheck || nextMission.activeCheck;
                            attachments = nextMission.email.attachments;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Extension/Vanilla Mission " + next + " errored out: " + ex);
                    }
                }
                else
                {
                    next = next.Substring(next.IndexOf(':') + 1);
                    var nextMission = CreateInstance(next, new Dictionary<string, string>());
                    if (nextMission != null)
                    {
                        activeCheck = activeCheck || nextMission.activeCheck;
                        attachments = nextMission.email.attachments;
                    }
                }
                var sender = "ERRORBOT";
                var subject = "ERROR";
                var body = "ERROR :: MAIL LOAD FAILED";
                while (reader.Name != "email" || reader.Name != "endFunc")
                    reader.Read();
                if (reader.Name == "email")
                {
                    if (reader.MoveToAttribute("sender"))
                       sender = Folder.deFilter(reader.ReadContentAsString());
                    if (reader.MoveToAttribute("subject"))
                        subject = Folder.deFilter(reader.ReadContentAsString());
                    reader.MoveToContent();
                    body = Folder.deFilter(reader.ReadContentAsString());
                }
                result = CreateInstance(missionId, storedObjects, next, new MailServer.EMailData(sender, body, subject, attachments));
                result.reloadGoalsSourceFile = next;
                while (reader.Name != "endFunc")
                    reader.Read();
                reader.MoveToAttribute ("val");
                result.endFunctionValue = reader.ReadContentAsInt();
                reader.MoveToAttribute ("name");
                result.endFunctionName = reader.ReadContentAsString();
                while (reader.Name != "posting")
                    reader.Read();
                reader.MoveToAttribute("title");
                result.postingTitle = Folder.deFilter(reader.ReadContentAsString());
                reader.MoveToContent();
                result.postingBody = Folder.deFilter(reader.ReadContentAsString());

                foreach (var s in moddedGoals)
                    result.AddGoal(s);
            }
            return result;
        }
    }
}
