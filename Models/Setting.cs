using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System;
using System.Text.Json.Serialization;

namespace Coflnet.Sky.Settings.Models
{
    /// <summary>
    /// One setting consisting of key, value and meta data
    /// </summary>
    [DataContract]
    public class Setting
    {
        /// <summary>
        /// The db id of this entry
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        [IgnoreDataMember]
        public int Id { get; set; }
        /// <summary>
        /// Key / external identifier
        /// </summary>
        /// <value></value>
        [DataMember(Name = "key")]
        [MaxLength(64)]
        public string Key { get; set; }
        /// <summary>
        /// Value of the setting
        /// </summary>
        /// <value></value>
        [DataMember(Name = "value")]
        public string Value { get; set; }
        /// <summary>
        /// The count of changes used for conflict resolution
        /// </summary>
        [DataMember(Name = "changeIndex")]
        public long ChangeIndex { get; set; }
        /// <summary>
        /// When this entry was last updated
        /// </summary>
        /// <value></value>
        [System.ComponentModel.DataAnnotations.Timestamp]
        [DataMember(Name = "lastUpdate")]
        public DateTime LastUpdate { get; set; }
        /// <summary>
        /// The user this setting belongs to
        /// </summary>
        [IgnoreDataMember]
        [JsonIgnore]
        public User User { get; set; }
    }
}