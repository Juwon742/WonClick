using System;

namespace WonClick.AutoParameter.Models
{
    public class InstanceParameterItem
    {
        public long ParameterId { get; set; }

        public string Name { get; set; } =
            string.Empty;

        public Guid? SharedParameterGuid { get; set; }

        public string StorageTypeName { get; set; } =
            string.Empty;


        public override string ToString()
        {
            return Name;
        }
    }
}