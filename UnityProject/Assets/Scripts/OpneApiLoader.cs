using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniJSON;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine.Networking;

public class OpneApiLoader : MonoBehaviour
{
    struct ModelProperty
    {
        public string Type;
        public string PrivateName;
        public string PublicName;
        public ModelProperty(string type, string privateName, string publicName)
        {
            this.Type = type;
            this.PrivateName = privateName;
            this.PublicName = publicName;
        }
    }
    struct ModelInformation
    {
        public string ModelName;
        public List<ModelProperty> Properties;
        public ModelInformation(string modelName, List<ModelProperty> properties)
        {
            this.ModelName = modelName;
            this.Properties = properties;
        }
    }


    const string SAVE_FOLDER_NAME = "変換して作ったModel達はここ";
#if UNITY_EDITOR
    string FilePath => Application.dataPath;
#else
string FilePath => AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
#endif

    [SerializeField]
    bool createInteraface = true;
    Action<string> erorrAction;
    Action sccessAction;

    public IEnumerator GetJsonAndCreateModel(string url, bool createInteraface,Action<string> erorrAction,Action sccessAction)
    {
        this.erorrAction = erorrAction;
        this.sccessAction = sccessAction;

        this.createInteraface = createInteraface;
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            this.erorrAction.Invoke(www.error);
            throw new Exception(www.error);
        }
        else
        {
            try
            {
                CSharpCreateModelsFromOpenApiJson(www.downloadHandler.text);
            }catch(Exception e)
            {
                this.erorrAction.Invoke(e.ToString());
            }            
            this.sccessAction.Invoke();
            Debug.Log("Create Models Success!!");
        }
    }

    void CSharpCreateModelsFromOpenApiJson(string json)
    {
        CreateFolder(FilePath + "/" + SAVE_FOLDER_NAME);
        Dictionary<string, object> dict = Json.Deserialize(json) as Dictionary<string, object>;
        var componentsDict = (Dictionary<string, object>)dict["components"];
        var schemasDict = (Dictionary<string, object>)componentsDict["schemas"];

        foreach (var model in schemasDict)
        {
            var modelInfo = GetModelInfoFromOpenApiModel(model);
            CreateCSharpCodeModel(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo, createInteraface);
            if (createInteraface)
            {
                CreateCSharpCodeInterface(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo);
            }
        }
    }


    void CreateCSharpCodeModel(string savePath, ModelInformation modelInfomation, bool createInteraface)
    {
        string newSavePath = savePath + "/Model";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + modelInfomation.ModelName + ".cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine("[System.Serializable]");
            writer.WriteLine("public class "+ modelInfomation.ModelName + (createInteraface?":I"+ modelInfomation.ModelName : ""));
            writer.WriteLine("{" + Environment.NewLine);
            foreach (var p in modelInfomation.Properties)
            {
                writer.WriteLine("  "+"[SerializeField]");
                writer.WriteLine("  " + p.Type + " " + p.PrivateName+";");
                writer.WriteLine("  " + "public " + p.Type + " " + p.PublicName + " => "+ p.PrivateName+";");
                writer.WriteLine(Environment.NewLine);
            }
            writer.WriteLine("}");
        }
    }
    void CreateCSharpCodeInterface(string savePath, ModelInformation modelInfomation)
    {
        string newSavePath = savePath + "/Interface";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/I" + modelInfomation.ModelName + ".cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine("public interface I" + modelInfomation.ModelName);
            writer.WriteLine("{" + Environment.NewLine);
            foreach (var p in modelInfomation.Properties)
            {
                writer.WriteLine("  " + p.Type + " " + p.PublicName + " { get; }");
                writer.WriteLine(Environment.NewLine);
            }
            writer.WriteLine("}");
        }
    }


    ModelInformation GetModelInfoFromOpenApiModel(KeyValuePair<string, object> model)
    {
        List<ModelProperty> properties = new List<ModelProperty>();
        string modelName = model.Key;
        var modelDict = (Dictionary<string, object>)model.Value;
        if(!((Dictionary<string, object>)modelDict).ContainsKey("properties"))
        {
            return new ModelInformation(modelName, properties);
        }

        var modelPropertyDict = (Dictionary<string, object>)modelDict["properties"];
        foreach (var modelProperty in modelPropertyDict)
        {
            var proPertyName = modelProperty.Key;
            string proPertyPrivateName = proPertyName.ToLower();
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            string proPertyPublicName = ti.ToTitleCase(proPertyName);
            if (proPertyPrivateName == proPertyPublicName)
            {
                proPertyPublicName = "_" + proPertyPublicName;
            }
            var openApiModelPropertyType = "";
            if (((Dictionary<string, object>)modelProperty.Value).ContainsKey("type"))
            {
                openApiModelPropertyType = (string)((Dictionary<string, object>)modelProperty.Value)["type"];
            }
            else
            {
                openApiModelPropertyType = "object";
            }
            string modelPropertyType = GetModelTypeFromOpenApiType(openApiModelPropertyType, modelProperty);
            properties.Add(new ModelProperty(modelPropertyType, proPertyPrivateName, proPertyPublicName));
        }
        return new ModelInformation(modelName, properties);
    }


    string GetModelTypeFromOpenApiType(string openApiModelPropertyType,KeyValuePair<string,object> modelProperty)
    {
        string modelPropertyType = "";
        if (openApiModelPropertyType == "integer")
        {
            modelPropertyType = "int";
        }
        else if (openApiModelPropertyType == "boolean")
        {
            modelPropertyType = "bool";
        }
        else if (openApiModelPropertyType == "object")
        {
            var propertyItems = (Dictionary<string, object>)modelProperty.Value;
            var objectTypeNameArry = ((string)propertyItems["$ref"]).Split('/');
            var objectTypeName = objectTypeNameArry[objectTypeNameArry.Length - 1];

            modelPropertyType = objectTypeName;
        }
        else if (openApiModelPropertyType == "array")
        {
            var propertyItems = (Dictionary<string, object>)(((Dictionary<string, object>)modelProperty.Value)["items"]);
            if (propertyItems.ContainsKey("type"))
            {
                modelPropertyType = "List<" + (string)propertyItems["type"] + ">";
            }
            else
            {
                var objectTypeNameArry = ((string)propertyItems["$ref"]).Split('/');
                var objectTypeName = objectTypeNameArry[objectTypeNameArry.Length - 1];

                modelPropertyType = "List<" + objectTypeName + ">";
            }
        }
        else
        {
            modelPropertyType = openApiModelPropertyType;
        }
        return modelPropertyType;
    }

    void CreateFolder(string path)
    {
        if (Directory.Exists(path))
        {
            //Console.WriteLine("フォルダがすでにアルヨ");
        }
        else
        {
            
            DirectoryInfo di = new DirectoryInfo(path);
            di.Create();
        }
    }

}

