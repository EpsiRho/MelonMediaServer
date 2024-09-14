using Melon.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Melon.LocalClasses
{
    public static class Storage
    {
        public static T LoadConfigFile<T>(string filename, string[] protectedProperties, out bool converted)
        {
            converted = false;
            if (!Directory.Exists($"{StateManager.melonPath}/Configs/"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Configs/");
            }

            string txt = "";
            try
            {
                txt = File.ReadAllText($"{StateManager.melonPath}/Configs/{filename}.json");
            }
            catch (Exception)
            {
                return default(T);
            }

            T config = default(T);

            try
            {
                config = JsonConvert.DeserializeObject<T>(txt);
            }
            catch (Exception)
            {
                config = TryConvertConfigType<T>(txt);
                converted = true;
            }

            if (config == null)
            {
                return default(T);
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();

            var instance = ActivatorUtilities.CreateInstance<Security>(services);

            if (protectedProperties != null)
            {
                foreach (var propertyName in protectedProperties)
                {
                    // Get the property info for the current property name
                    PropertyInfo propertyInfo = typeof(T).GetProperty(propertyName);

                    // Check if the property was found and is not null
                    if (propertyInfo != null)
                    {
                        if (propertyInfo.PropertyType == typeof(string))
                        {
                            var data = (string)propertyInfo.GetValue(config);
                            if (data == null)
                            {
                                propertyInfo.SetValue(config, "");
                                continue;
                            }
                            var unprotectedData = instance._protector.Unprotect(data);
                            propertyInfo.SetValue(config, unprotectedData);
                        }
                        else if (propertyInfo.PropertyType == typeof(byte[]))
                        {
                            var data = (byte[])propertyInfo.GetValue(config);
                            if(data == null)
                            {
                                propertyInfo.SetValue(config, null);
                                continue;
                            }
                            var unprotectedData = instance._protector.Unprotect(data);
                            propertyInfo.SetValue(config, unprotectedData);
                        }
                    }
                }
            }

            return config;
        }
        public static void SaveConfigFile<T>(string filename, T config, string[] protectedProperties)
        {
            if (!Directory.Exists($"{StateManager.melonPath}/Configs/"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Configs/");
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();
            var instance = ActivatorUtilities.CreateInstance<Security>(services);

            if (protectedProperties != null)
            {
                foreach (var propertyName in protectedProperties)
                {
                    // Get the property info for the current property name
                    PropertyInfo propertyInfo = typeof(T).GetProperty(propertyName);

                    // Check if the property was found and is not null
                    if (propertyInfo != null)
                    {
                        if (propertyInfo.PropertyType == typeof(string))
                        {
                            var data = (string)propertyInfo.GetValue(config);
                            if (data == null)
                            {
                                propertyInfo.SetValue(config, "");
                                continue;
                            }
                            var protectedData = instance._protector.Protect(data);
                            propertyInfo.SetValue(config, protectedData);
                        }
                        else if (propertyInfo.PropertyType == typeof(byte[]))
                        {
                            var data = (byte[])propertyInfo.GetValue(config);
                            if (data == null)
                            {
                                propertyInfo.SetValue(config, null);
                                continue;
                            }
                            var protectedData = instance._protector.Protect(data);
                            propertyInfo.SetValue(config, protectedData);
                        }
                    }
                }
            }

            string settingstxt = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText($"{StateManager.melonPath}/Configs/{filename}.json", settingstxt);

        }
        private static T TryConvertConfigType<T>(string json)
        {
            var dynDoc = JsonConvert.DeserializeObject<dynamic>(json);
            try
            {
                T objRes = DbVersionManager.ConvertDynamicObject<T>(dynDoc);
                return objRes;
            }
            catch (Exception)
            {
                return default(T);
            }
        }
        public static bool PropertiesEqual<T>(this T self, T to, params string[] ignore) where T : class
        {
            if (self != null && to != null)
            {
                Type type = typeof(T);
                List<string> ignoreList = new List<string>(ignore);
                foreach (System.Reflection.PropertyInfo pi in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (!ignoreList.Contains(pi.Name))
                    {
                        object selfValue = type.GetProperty(pi.Name).GetValue(self, null);
                        object toValue = type.GetProperty(pi.Name).GetValue(to, null);

                        if(selfValue.GetType() == typeof(string[]) || selfValue.GetType() == typeof(List<string>))
                        {
                            var check = Enumerable.SequenceEqual((IEnumerable<string>)selfValue, (IEnumerable<string>)toValue);
                            if(check == false)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue)))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            return self == to;
        }
    }
}
