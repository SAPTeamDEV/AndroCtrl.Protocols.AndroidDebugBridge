﻿// <copyright file="InfoReceiver.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion, SAP Team">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion, Alireza Poodineh. All rights reserved.
// </copyright>


using System;
using System.Collections.Generic;

using SAPTeam.AndroCtrl.Adb.Receivers;

namespace SAPTeam.AndroCtrl.Adb.DeviceCommands
{
    /// <summary>
    /// Processes command line output of a <c>adb</c> shell command.
    /// </summary>
    public class InfoReceiver : MultiLineReceiver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InfoReceiver"/> class.
        /// </summary>
        public InfoReceiver()
        {
            Properties = new Dictionary<string, object>();
            PropertyParsers = new Dictionary<string, Func<string, object>>();
        }

        /// <summary>
        /// Gets or sets a dictionary with the extracted properties and their corresponding values.
        /// </summary>
        private Dictionary<string, object> Properties
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the dictionary with all properties and their corresponding property parsers.
        /// A property parser extracts the property value out of a <see cref="string"/> if possible.
        /// </summary>
        private Dictionary<string, Func<string, object>> PropertyParsers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the value of the property out of the Properties dictionary. Returns null if the property is not present in the directory.
        /// </summary>
        /// <param name="propertyName">The name of the property</param>
        /// <returns>The received value</returns>
        public object GetPropertyValue(string propertyName)
        {
            return Properties.ContainsKey(propertyName) ? Properties[propertyName] : null;
        }

        /// <summary>
        /// Adds a new parser to this receiver. The parsers parses one received line and extracts the property value if possible.
        /// The parser should return <c>null</c> if the property value cannot be found in the line.
        /// </summary>
        /// <param name="property">The property corresponding with the parser.</param>
        /// <param name="parser">Function parsing one string and returning the property value if possible. </param>
        public void AddPropertyParser(string property, Func<string, object> parser)
        {
            PropertyParsers.Add(property, parser);
        }

        /// <summary>
        /// Processes the new lines, and sets version information if the line represents package information data.
        /// </summary>
        /// <param name="lines">
        /// The lines to process.
        /// </param>
        protected override void ProcessNewLines(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                if (line == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, Func<string, object>> parser in PropertyParsers)
                {
                    object propertyValue = parser.Value(line);
                    if (propertyValue != null)
                    {
                        Properties.Add(parser.Key, propertyValue);
                    }
                }
            }
        }
    }
}