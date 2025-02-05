﻿// <copyright file="VersionInfo.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion, SAP Team">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion, Alireza Poodineh. All rights reserved.
// </copyright>

namespace SAPTeam.AndroCtrl.Adb.DeviceCommands
{
    /// <summary>
    /// Represents a version of an Android application.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VersionInfo"/> class.
        /// </summary>
        /// <param name="versionCode">The version code of the application.</param>
        /// <param name="versionName">The version name of the application</param>
        public VersionInfo(int versionCode, string versionName)
        {
            VersionCode = versionCode;
            VersionName = versionName;
        }

        /// <summary>
        /// Gets or sets the version code of an Android application
        /// </summary>
        public int VersionCode
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the version name of an Android application
        /// </summary>
        public string VersionName
        {
            get;
            set;
        }
    }
}