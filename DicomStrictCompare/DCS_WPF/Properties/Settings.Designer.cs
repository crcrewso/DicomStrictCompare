﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DCS_WPF.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.4.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("c:\\Temp\\DSCsave")]
        public string DefaultSaveDirectory {
            get {
                return ((string)(this["DefaultSaveDirectory"]));
            }
            set {
                this["DefaultSaveDirectory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Output")]
        public string DefaultSaveLabel {
            get {
                return ((string)(this["DefaultSaveLabel"]));
            }
            set {
                this["DefaultSaveLabel"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("c:\\Temp\\Source")]
        public string DefaultSourceDirectory {
            get {
                return ((string)(this["DefaultSourceDirectory"]));
            }
            set {
                this["DefaultSourceDirectory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Reference")]
        public string DefaultSourceLabel {
            get {
                return ((string)(this["DefaultSourceLabel"]));
            }
            set {
                this["DefaultSourceLabel"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("c:\\Temp\\Target")]
        public string DefaultTargetDirectory {
            get {
                return ((string)(this["DefaultTargetDirectory"]));
            }
            set {
                this["DefaultTargetDirectory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Investigation")]
        public string DefaultTargetLabel {
            get {
                return ((string)(this["DefaultTargetLabel"]));
            }
            set {
                this["DefaultTargetLabel"] = value;
            }
        }
    }
}
