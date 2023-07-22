using Newtonsoft.Json.Linq;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LLC_To_Paratranz
{
	internal class Error_logger
    {
        private readonly string logFilePath;
        StreamWriter LogStreamWriter
        {
            get
            {
                if (_logStreamWriter == null)
                    _logStreamWriter = new StreamWriter(logFilePath, append: true, Encoding.UTF8);
                return _logStreamWriter;
            }
        }
        StreamWriter _logStreamWriter;
        public Error_logger(string logFilePath)
        {
            FileInfo fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Exists)
                fileInfo.Delete();
            this.logFilePath = logFilePath;
        }

        public void LogError(string message)
        {
            string logMessage = $"[{DateTime.Now}] {message}{Environment.NewLine}";
            LogStreamWriter.WriteLine(logMessage);
            Console.WriteLine(logMessage);
        }
    }
    public static class Program
    {
        static string Localize_Path;
        static string ParatranzWrok_Path;
        static int Localize_Path_Length;
        static int ParatranzWrok_Path_Length;

        static readonly Error_logger logger = new Error_logger("./Error.txt");
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((object o, UnhandledExceptionEventArgs e) => { logger.LogError(o.ToString() + e.ToString()); });
            try
            {
                Localize_Path = new DirectoryInfo("./Localize").FullName;
                Localize_Path_Length = Localize_Path.Length + 3;
                ParatranzWrok_Path = new DirectoryInfo("./utf8/Localize").FullName;
                ParatranzWrok_Path_Length = ParatranzWrok_Path.Length;
                LoadGitHubWroks(new DirectoryInfo(Localize_Path + "/KR"), kr_dic);
                var RawNickNameObj = JSON.Parse(File.ReadAllText(Localize_Path + "/NickName.json")).AsObject;
                cn_dic["/RawNickName.json"] = RawNickNameObj;

                LoadParatranzWroks(new DirectoryInfo(ParatranzWrok_Path), pt_dic);
                ToGitHubWrok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }
        public static Dictionary<string, JSONObject> cn_dic = new Dictionary<string, JSONObject>();
        public static Dictionary<string, JSONObject> kr_dic = new Dictionary<string, JSONObject>();
        public static Dictionary<string, JSONArray> pt_dic = new Dictionary<string, JSONArray>();
        public static void LoadGitHubWroks(DirectoryInfo directory, Dictionary<string, JSONObject> dic)
        {
            foreach (FileInfo fileInfo in directory.GetFiles())
            {
                var value = File.ReadAllText(fileInfo.FullName);
                string fileName = fileInfo.DirectoryName.Remove(0, Localize_Path_Length) + "/" + fileInfo.Name.Remove(0, 3);
                dic[fileName] = JSON.Parse(value).AsObject;
            }
            foreach (DirectoryInfo directoryInfo in directory.GetDirectories())
                LoadGitHubWroks(directoryInfo, dic);
        }
        public static void LoadParatranzWroks(DirectoryInfo directory, Dictionary<string, JSONArray> dic)
        {
            foreach (FileInfo fileInfo in directory.GetFiles())
            {
                var value = File.ReadAllText(fileInfo.FullName);
                string fileName = fileInfo.DirectoryName.Remove(0, ParatranzWrok_Path_Length) + "/" + fileInfo.Name;
                dic[fileName] = JSON.Parse(value).AsArray;
            }
            foreach (DirectoryInfo directoryInfo in directory.GetDirectories())
                LoadParatranzWroks(directoryInfo, dic);
        }
        public static void ToGitHubWrok()
        {
            if (Directory.Exists(Localize_Path + "/CN"))
                Directory.Delete(Localize_Path + "/CN", true);
            Directory.CreateDirectory(Localize_Path + "/CN");
            kr_dic["/NickName.json"] = cn_dic["/RawNickName.json"];
            foreach (var pt_kvs in pt_dic)
            {
                var pt = pt_kvs.Value.List.ToDictionary(key => key[0].Value, value => value.AsObject);
                if (kr_dic.TryGetValue(pt_kvs.Key, out var kr))
                {
                    var krobjs = kr[0].AsArray;
                    for (int i = 0; i < krobjs.Count; i++)
                    {
                        var krobj = krobjs[i].AsObject;
                        string ObjectId = krobj[0];
                        foreach (var keyValue in krobj.Dict.ToArray())
                        {
                            if (!keyValue.Value.IsNumber && keyValue.Key != "id" && keyValue.Key != "model" && keyValue.Key != "usage")
                            {
                                if (pt.TryGetValue(ObjectId + "-" + keyValue.Key, out var ptobj))
                                {
                                    if (!ptobj.Dict.TryGetValue("translation", out var translation) || string.IsNullOrEmpty(translation))
                                        continue;
                                    if (keyValue.Value.IsString)
                                        krobj[keyValue.Key].Value = ptobj[2].Value.Replace("\\n", "\n");
                                    else
                                    {

                                        string value = ptobj[2].Value;
                                        try
                                        {
                                            krobj.Dict[keyValue.Key] = JSON.Parse(value);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex.ToString());
                                            logger.LogError($"Json Error! File:{pt_kvs.Key} ;Key:{keyValue.Key} ;Value:{value}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    string krjson = kr.ToString();
                    string filePath = Localize_Path + "/CN" + pt_kvs.Key;
                    string directoryPath = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directoryPath))
                        Directory.CreateDirectory(directoryPath);
                    File.WriteAllText(filePath, JObject.Parse(krjson).ToString());
                }
            }
        }
    }
}
