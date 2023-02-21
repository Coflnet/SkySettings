using System;

namespace Coflnet.Sky.Settings.Models
{
    /// <summary>
    /// Setting stored in cassandra
    /// </summary>
    public class CassandraSetting
    {
        /// <summary>
        /// settings key
        /// </summary>
        [Cassandra.Mapping.Attributes.ClusteringKey(0)]
        public string Key { get; set; }
        /// <summary>
        /// Value of the setting
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// The count of changes used for conflict resolution
        /// </summary>
        public long ChangeIndex { get; set; }
        /// <summary>
        /// When this entry was last updated
        /// </summary>
        public DateTime LastUpdate { get; set; }
        /// <summary>
        /// The userId this setting belongs to
        /// </summary>
        [Cassandra.Mapping.Attributes.PartitionKey]
        public string UserId { get; set; }
    }
}