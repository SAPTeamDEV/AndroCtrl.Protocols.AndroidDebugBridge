﻿// <copyright file="ProcessOutputReceiver.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion, SAP Team">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion, Alireza Poodineh. All rights reserved.
// </copyright>


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SAPTeam.AndroCtrl.Adb.DeviceCommands;

namespace SAPTeam.AndroCtrl.Adb.Receivers
{
    /// <summary>
    /// Parses the output of a <c>cat /proc/[pid]/stat</c> command.
    /// </summary>
    internal class ProcessOutputReceiver : MultiLineReceiver
    {
        /// <summary>
        /// Gets a list of all processes that have been received.
        /// </summary>
        public Collection<AndroidProcess> Processes
        { get; private set; } = new Collection<AndroidProcess>();

        /// <inheritdoc/>
        protected override void ProcessNewLines(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                // Process has already died (e.g. the cat process itself)
                if (line.Contains("No such file or directory"))
                {
                    continue;
                }

                try
                {
                    Processes.Add(AndroidProcess.Parse(line, cmdLinePrefix: true));
                }
                catch (Exception)
                {
                    // Swallow
                }
            }
        }
    }
}