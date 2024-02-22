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
        public static T LoadConfigFile<T>(string filename, string[] protectedProperties)
        {
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
            T config = JsonConvert.DeserializeObject<T>(txt);

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
                            var unprotectedData = instance._protector.Unprotect(data);
                            propertyInfo.SetValue(config, unprotectedData);
                        }
                        else if (propertyInfo.PropertyType == typeof(byte[]))
                        {
                            var data = (byte[])propertyInfo.GetValue(config);
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
                            var unprotectedData = instance._protector.Protect(data);
                            propertyInfo.SetValue(config, unprotectedData);
                        }
                        else if (propertyInfo.PropertyType == typeof(byte[]))
                        {
                            var data = (byte[])propertyInfo.GetValue(config);
                            var unprotectedData = instance._protector.Protect(data);
                            propertyInfo.SetValue(config, unprotectedData);
                        }
                    }
                }
            }

            string settingstxt = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText($"{StateManager.melonPath}/Configs/{filename}.json", settingstxt);

        }
    }
}
