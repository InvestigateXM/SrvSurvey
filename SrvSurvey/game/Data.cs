﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SrvSurvey.game
{
    /// <summary>
    /// A base class for various data file classes
    /// </summary>
    internal abstract class Data
    {
        protected string filepath;

        protected static T? Load<T>(string filepath) where T : Data
        {
            // read and parse file contents into tmp object
            if (File.Exists(filepath))
            {
                var json = File.ReadAllText(filepath);
                try
                {
                    var data = JsonConvert.DeserializeObject<T>(json)!;
                    Game.log($"Loaded data from: {filepath}");
                    data.filepath = filepath;
                    return data;
                }
                catch (Exception ex)
                {
                    Game.log($"Failed to read data: {ex.Message}");
                    Game.log(json);
                }
            }

            return null;
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(this.filepath, json);
        }
    }
}