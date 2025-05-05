using System;
using System.Configuration;
using System.IO;
using System.Xml.Serialization;
namespace AlexAssistant.Properties
{
    public class Settings : ApplicationSettingsBase
    {
        private static readonly Settings defaultInstance = new Settings();
        public static Settings Default
        {
            get { return defaultInstance; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string UserName
        {
            get { return (string)this["UserName"]; }
            set { this["UserName"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool AssistantEnabled
        {
            get { return (bool)this["AssistantEnabled"]; }
            set { this["AssistantEnabled"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int MicrophoneIndexSet
        {
            get { return (int)this["MicrophoneIndexSet"]; }
            set { this["MicrophoneIndexSet"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string MicrophoneName
        {
            get { return (string)this["MicrophoneName"]; }
            set { this["MicrophoneName"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int CameraIndex
        {
            get { return (int)this["CameraIndex"]; }
            set { this["CameraIndex"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool NotificationsEnabled
        {
            get { return (bool)this["NotificationsEnabled"]; }
            set { this["NotificationsEnabled"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("")]
        [SettingsManageability(SettingsManageability.Roaming)]
        public string OverlayPath
        {
            get { return ((string)(this["OverlayPath"])); }
            set { this["OverlayPath"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool SaveConfidence
        {
            get { return (bool)this["SaveConfidence"]; }
            set { this["SaveConfidence"] = value; }
        }
        [UserScopedSetting]
        [DefaultSettingValue("false")]
        public bool DontShowTrayNotification
        {
            get { return (bool)this["DontShowTrayNotification"]; }
            set { this["DontShowTrayNotification"] = value; }
        }

    }
}