using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimPc
{
    public class AppSettings : ApplicationSettingsBase
    {
        [UserScopedSetting]
        [DefaultSettingValue("Dark")]
        public string Theme
        {
            get { return (string)this["Theme"]; }
            set { this["Theme"] = value; }
        }
    }
}
