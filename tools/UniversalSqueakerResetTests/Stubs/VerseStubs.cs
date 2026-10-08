#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Verse
{
    public interface IExposable
    {
        void ExposeData();
    }

    public enum LoadSaveMode { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }

    public enum LookMode { Undefined, Value, Deep, Reference, Def }

    public abstract class ModSettings
    {
        public virtual void ExposeData()
        {
        }
    }

    public struct FloatRange
    {
        public readonly float min;
        public readonly float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public static FloatRange One => new(1f, 1f);

        public override string ToString() => "(" + min.ToString("G9") + "," + max.ToString("G9") + ")";
    }

    public class Def
    {
        public string defName = "";
        public ModContentPack modContentPack = null;

        public virtual IEnumerable<string> ConfigErrors()
        {
            yield break;
        }
    }

    public class ModContentPack
    {
        public ModMetaData ModMetaData = new();
    }

    public class ModMetaData
    {
        public string PackageIdNonUnique = "";
    }

    namespace Sound
    {
        public enum SoundContext
        {
            Any,
            MapOnly,
        }

        public class SoundDef
        {
            public string defName = "";
            public SoundContext context = SoundContext.Any;
            public List<SubSoundDef> subSounds = new();
        }

        public class SubSoundDef
        {
            public bool onCamera;
            public List<object> grains = new();
        }
    }

    public enum ProgramState
    {
        Playing,
        MapInitializing,
    }

    public class Game
    {
    }

    public class Root_Play
    {
    }

    public static class Current
    {
        public static ProgramState ProgramState = ProgramState.Playing;
        public static Game Game = null;
        public static object Root = null;
    }

    public class Thing
    {
    }

    public class Pawn : Thing
    {
        public bool Spawned;
        public Map MapHeld = null;

        public T TryGetComp<T>() where T : class => null;
    }

    public class Map
    {
    }

    public class TickManager
    {
        public int TicksGame;
        public float TickRateMultiplier;
    }

    public class MapUI
    {
    }

    public class Selector
    {
        public Thing SingleSelectedThing = null;
    }

    public static class Find
    {
        public static TickManager TickManager = null;
        public static Map CurrentMap = null;
        public static MapUI MapUI = null;
        public static Selector Selector = null;
    }

    public static class GenFilePaths
    {
        public static string ConfigFolderPath { get; set; } = Path.GetTempPath();
    }

    public static class GenText
    {
        public static string SanitizeFilename(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }
    }

    public static class SafeSaver
    {
        public static void Save(string path, string documentElementName, Action saveAction)
        {
            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            Scribe.saver.InitSaving(documentElementName);
            saveAction();
            string xml = Scribe.saver.FinalizeSaving();
            File.WriteAllText(tmp, xml);
            File.Delete(path);
            File.Move(tmp, path);
        }
    }

    public static class Scribe
    {
        public static LoadSaveMode mode = LoadSaveMode.Inactive;
        public static ScribeSaver saver = new();
        public static ScribeLoader loader = new();

        public static bool EnterNode(string nodeName)
        {
            return mode == LoadSaveMode.Saving ? saver.EnterNode(nodeName) : loader.EnterNode(nodeName);
        }

        public static void ExitNode()
        {
            if (mode == LoadSaveMode.Saving) saver.ExitNode();
            else loader.ExitNode();
        }
    }

    public sealed class ScribeSaver
    {
        private sealed class Utf8StringWriter : StringWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        }

        private Utf8StringWriter stream = null;
        private XmlWriter writer = null;

        public void InitSaving(string documentElementName)
        {
            Scribe.mode = LoadSaveMode.Saving;
            stream = new Utf8StringWriter();
            XmlWriterSettings settings = new()
            {
                Indent = true,
                IndentChars = "\t",
            };
            writer = XmlWriter.Create(stream, settings);
            writer.WriteStartDocument();
            EnterNode(documentElementName);
        }

        public string FinalizeSaving()
        {
            ExitNode();
            writer.WriteEndDocument();
            writer.Flush();
            writer.Close();
            Scribe.mode = LoadSaveMode.Inactive;
            string text = stream.ToString();
            stream.Dispose();
            return text;
        }

        public bool EnterNode(string nodeName)
        {
            if (writer == null) return false;
            writer.WriteStartElement(nodeName);
            return true;
        }

        public void ExitNode()
        {
            if (writer != null) writer.WriteEndElement();
        }

        public void WriteElement(string elementName, string value) => writer.WriteElementString(elementName, value);

        public void WriteAttribute(string attributeName, string value) => writer.WriteAttributeString(attributeName, value);
    }

    public sealed class ScribeLoader
    {
        private XmlNode root = null;
        private readonly Stack<XmlNode> parentStack = new();

        public XmlNode curXmlParent { get; set; } = null;

        public void InitLoading(string filePath)
        {
            XmlDocument doc = new();
            doc.Load(filePath);
            root = doc.DocumentElement;
            curXmlParent = root;
            parentStack.Clear();
            Scribe.mode = LoadSaveMode.LoadingVars;
        }

        public void FinalizeLoading()
        {
            foreach (IExposable exposable in ScribeExtractor.PostLoadInitQueue.ToArray())
            {
                Scribe.mode = LoadSaveMode.PostLoadInit;
                exposable.ExposeData();
            }
            ScribeExtractor.PostLoadInitQueue.Clear();
            Scribe.mode = LoadSaveMode.Inactive;
        }

        public bool EnterNode(string label)
        {
            XmlNode child = curXmlParent[label];
            if (child == null) return false;
            parentStack.Push(curXmlParent);
            curXmlParent = child;
            return true;
        }

        public void ExitNode()
        {
            curXmlParent = parentStack.Count > 0 ? parentStack.Pop() : root;
        }
    }

    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string label, T defaultValue = default, bool forceSave = false)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (!forceSave && (value != null || defaultValue == null))
                {
                    if (value == null) return;
                    if (value.Equals(defaultValue)) return;
                }
                if (value == null)
                {
                    if (!Scribe.EnterNode(label)) return;
                    try { Scribe.saver.WriteAttribute("IsNull", "True"); }
                    finally { Scribe.ExitNode(); }
                    return;
                }
                Scribe.saver.WriteElement(label, value switch
                {
                    float num => num.ToString("G9"),
                    _ => value.ToString(),
                });
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                value = ScribeExtractor.ValueFromNode(Scribe.loader.curXmlParent[label], defaultValue);
            }
        }
    }

    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> list, string label, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs)
        {
            if (Scribe.EnterNode(label))
            {
                try
                {
                    if (Scribe.mode == LoadSaveMode.Saving)
                    {
                        if (list == null) { Scribe.saver.WriteAttribute("IsNull", "True"); return; }
                        foreach (T item in list)
                        {
                            switch (lookMode)
                            {
                                case LookMode.Value:
                                {
                                    T value = item;
                                    Scribe_Values.Look(ref value, "li", default, forceSave: true);
                                    break;
                                }
                                case LookMode.Deep:
                                {
                                    T target = item;
                                    Scribe_Deep.Look(ref target, "li", ctorArgs);
                                    break;
                                }
                            }
                        }
                    }
                    else if (Scribe.mode == LoadSaveMode.LoadingVars)
                    {
                        XmlAttribute isNull = Scribe.loader.curXmlParent.Attributes["IsNull"];
                        if (isNull != null && isNull.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                        {
                            list = null;
                        }
                        else
                        {
                            list = new List<T>(Scribe.loader.curXmlParent.ChildNodes.Count);
                            foreach (XmlNode child in Scribe.loader.curXmlParent.ChildNodes)
                            {
                                switch (lookMode)
                                {
                                    case LookMode.Value:
                                        list.Add(ScribeExtractor.ValueFromNode(child, default(T)));
                                        break;
                                    case LookMode.Deep:
                                        list.Add(ScribeExtractor.SaveableFromNode<T>(child, ctorArgs));
                                        break;
                                }
                            }
                        }
                    }
                }
                finally { Scribe.ExitNode(); }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                list = null;
            }
        }

        // Dictionary serialization is not exercised by this harness; the overload exists so the linked
        // production ExposeData surface compiles.
        public static void Look<TKey, TValue>(ref Dictionary<TKey, TValue> dict, string label, LookMode keyLookMode, LookMode valueLookMode)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (dict == null) return;
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                dict = new Dictionary<TKey, TValue>();
            }
        }
    }

    public static class Scribe_Deep
    {
        public static void Look<T>(ref T target, string label, params object[] ctorArgs)
        {
            if (Scribe.EnterNode(label))
            {
                try
                {
                    if (Scribe.mode == LoadSaveMode.Saving)
                    {
                        if (target != null)
                        {
                            IExposable exposable = (IExposable)target;
                            exposable.ExposeData();
                        }
                        else Scribe.saver.WriteAttribute("IsNull", "True");
                    }
                    else if (Scribe.mode == LoadSaveMode.LoadingVars)
                    {
                        target = ScribeExtractor.SaveableFromNode<T>(Scribe.loader.curXmlParent, ctorArgs);
                    }
                    else if (Scribe.mode == LoadSaveMode.PostLoadInit && target != null)
                    {
                        IExposable exposable = (IExposable)target;
                        exposable.ExposeData();
                    }
                }
                finally { Scribe.ExitNode(); }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                target = default;
            }
        }
    }

    public static class ScribeExtractor
    {
        public static readonly List<IExposable> PostLoadInitQueue = new();

        public static T ValueFromNode<T>(XmlNode subNode, T defaultValue)
        {
            if (subNode == null) return defaultValue;
            XmlAttribute isNull = subNode.Attributes["IsNull"];
            if (isNull != null && isNull.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase)) return default;
            try
            {
                return ParseHelper.FromString<T>(subNode.InnerText);
            }
            catch (Exception ex)
            {
                Log.Error("Exception parsing node " + subNode.OuterXml + " into a " + typeof(T) + ":\n" + ex);
                return default;
            }
        }

        public static T SaveableFromNode<T>(XmlNode subNode, object[] ctorArgs)
        {
            if (subNode == null) return default;
            XmlAttribute isNull = subNode.Attributes["IsNull"];
            if (isNull != null && isNull.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase)) return default;
            T instance = (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ctorArgs, null);
            IExposable exposable = (IExposable)instance;
            XmlNode previous = Scribe.loader.curXmlParent;
            Scribe.loader.curXmlParent = subNode;
            try { exposable.ExposeData(); }
            finally { Scribe.loader.curXmlParent = previous; }
            PostLoadInitQueue.Add(exposable);
            return instance;
        }
    }

    public static class ParseHelper
    {
        public static T FromString<T>(string str)
        {
            Type type = typeof(T);
            if (type == typeof(int)) return (T)(object)int.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return (T)(object)bool.Parse(str);
            if (type == typeof(string)) return (T)(object)str;
            if (type == typeof(float)) return (T)(object)float.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
            if (type == typeof(FloatRange))
            {
                // Minimal support for the two-number Scribe form used by settings fixtures.
                string[] parts = str.Trim('(', ')', ' ').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    return (T)(object)new FloatRange(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                return default;
            }
            if (type.IsEnum) return (T)Enum.Parse(type, str);
            throw new InvalidOperationException("Unsupported settings-migration parse type: " + type);
        }
    }

    public static class Log
    {
        public readonly struct Entry
        {
            public readonly string Level;
            public readonly string Text;
            public Entry(string level, string text) { Level = level; Text = text; }
        }

        public static readonly List<Entry> Captured = new();

        public static void Message(string text) => Captured.Add(new Entry("info", text));
        public static void Warning(string text) => Captured.Add(new Entry("warning", text));
        public static void Error(string text) => Captured.Add(new Entry("error", text));

        public static void Reset() => Captured.Clear();
    }
}
