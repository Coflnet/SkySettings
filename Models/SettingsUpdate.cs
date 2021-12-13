using System.Runtime.Serialization;

namespace Coflnet.Sky.Settings.Models
{
    [DataContract]
    public class SettingsUpdate
    {
        [DataMember(Name = "userId")]
        public string UserId { get; set; }
        [DataMember(Name = "key")]
        public string Key { get; set; }
        [DataMember(Name = "value")]
        public string Value { get; set; }
    }
}