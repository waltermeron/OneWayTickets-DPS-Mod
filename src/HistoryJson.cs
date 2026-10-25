using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OneWayDps {
    // The game already ships Json.NET. Unity JsonUtility omits these mod-defined generic lists.
    public static class HistoryJson {
        sealed class FieldsOnly : DefaultContractResolver {
            protected override IList<JsonProperty> CreateProperties(Type type,MemberSerialization memberSerialization) {
                var fields=new List<JsonProperty>();
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance)) {
                    var property=base.CreateProperty(field,MemberSerialization.Fields);
                    property.Ignored=false;property.Readable=true;property.Writable=true;
                    fields.Add(property);
                }
                return fields;
            }
        }
        static readonly JsonSerializerSettings settings=new JsonSerializerSettings {
            ContractResolver=new FieldsOnly(),TypeNameHandling=TypeNameHandling.None,
            ObjectCreationHandling=ObjectCreationHandling.Replace,Formatting=Formatting.Indented,
            CheckAdditionalContent=true,MaxDepth=32
        };
        public static string Encode(HistoryDatabase data){return JsonConvert.SerializeObject(data,settings);}
        public static HistoryDatabase Decode(string text){return JsonConvert.DeserializeObject<HistoryDatabase>(text,settings);}
    }
}
