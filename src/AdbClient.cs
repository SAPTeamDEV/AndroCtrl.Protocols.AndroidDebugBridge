﻿// <copyright file="AdbClient.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion, SAP Team">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion, Alireza Poodineh. All rights reserved.
// </copyright>


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAPTeam.AndroCtrl.Adb.Exceptions;
using SAPTeam.AndroCtrl.Adb.Interfaces;
using SAPTeam.AndroCtrl.Adb.Logs;
using SAPTeam.AndroCtrl.Adb.Receivers;

namespace SAPTeam.AndroCtrl.Adb
{
    /// <summary>
    /// <para>
    ///     Implements the <see cref="IAdbClient"/> interface, and allows you to interact with the
    ///     adb server and devices that are connected to that adb server.
    /// </para>
    /// <para>
    ///     For example, to fetch a list of all devices that are currently connected to this PC, you can
    ///     call the <see cref="GetDevices"/> method.
    /// </para>
    /// <para>
    ///     To run a command on a device, you can use the <see cref="ExecuteRemoteCommandAsync(string, DeviceData, IShellOutputReceiver, CancellationToken)"/>
    ///     method.
    /// </para>
    /// </summary>
    /// <seealso href="https://github.com/android/platform_system_core/blob/master/adb/SERVICES.TXT">SERVICES.TXT</seealso>
    /// <seealso href="https://github.com/android/platform_system_core/blob/master/adb/adb_client.c">adb_client.c</seealso>
    /// <seealso href="https://github.com/android/platform_system_core/blob/master/adb/adb.c">adb.c</seealso>
    public class AdbClient : IAdbClient
    {
        /// <summary>
        /// The default encoding
        /// </summary>
        public const string DefaultEncoding = "ISO-8859-1";

        /// <summary>
        /// The port at which the Android Debug Bridge server listens by default.
        /// </summary>
        public const int AdbServerPort = 5037;

        /// <summary>
        /// The default port to use when connecting to a device over TCP/IP.
        /// </summary>
        public const int DefaultPort = 5555;

        private readonly Func<EndPoint, IAdbSocket> adbSocketFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdbClient"/> class.
        /// </summary>
        public AdbClient()
            : this(new IPEndPoint(IPAddress.Loopback, AdbServerPort), Factories.AdbSocketFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdbClient"/> class.
        /// </summary>
        /// <param name="endPoint">
        /// The <see cref="EndPoint"/> at which the adb server is listening.
        /// </param>
        /// <param name="adbSocketFactory">
        /// The adb socket factory.
        /// </param>
        public AdbClient(EndPoint endPoint, Func<EndPoint, IAdbSocket> adbSocketFactory)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException();
            }

            if (!(endPoint is IPEndPoint || endPoint is DnsEndPoint))
            {
                throw new NotSupportedException("Only TCP endpoints are supported");
            }

            EndPoint = endPoint;
            this.adbSocketFactory = adbSocketFactory ?? throw new ArgumentNullException(nameof(adbSocketFactory));
        }

        /// <summary>
        /// Gets the encoding used when communicating with adb.
        /// </summary>
        public static Encoding Encoding
        { get; } = Encoding.GetEncoding(DefaultEncoding);

        internal static EndPoint DefaultEndPoint => new IPEndPoint(IPAddress.Loopback, DefaultPort);

        /// <summary>
        /// Gets the <see cref="EndPoint"/> at which the adb server is listening.
        /// </summary>
        public EndPoint EndPoint
        {
            get;
            private set;
        }

        /// <summary>
        /// Create an ASCII string preceded by four hex digits. The opening "####"
        /// is the length of the rest of the string, encoded as ASCII hex(case
        /// doesn't matter).
        /// </summary>
        /// <param name="req">The request to form.
        /// </param>
        /// <returns>
        /// An array containing <c>####req</c>.
        /// </returns>
        public static byte[] FormAdbRequest(string req)
        {
            string resultStr = string.Format("{0}{1}", req.Length.ToString("X4"), req);
            byte[] result = Encoding.GetBytes(resultStr);
            return result;
        }

        /// <summary>
        /// Creates the adb forward request.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <returns>
        /// This returns an array containing <c>"####tcp:{port}:{addStr}"</c>.
        /// </returns>
        public static byte[] CreateAdbForwardRequest(string address, int port)
        {
            string request = address == null ? "tcp:" + port : "tcp:" + port + ":" + address;
            return FormAdbRequest(request);
        }

        /// <inheritdoc/>
        public int GetAdbVersion()
        {
            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest("host:version");
                AdbResponse response = socket.ReadAdbResponse();
                string version = socket.ReadString();

                return int.Parse(version, NumberStyles.HexNumber);
            }
        }

        /// <inheritdoc/>
        public void KillAdb()
        {
            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest("host:kill");
            }

            // The host will immediately close the connection after the kill
            // command has been sent; no need to read the response.
        }

        /// <inheritdoc/>
        public List<DeviceData> GetDevices()
        {
            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest("host:devices-l");
                socket.ReadAdbResponse();
                string reply = socket.ReadString();

                string[] data = reply.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                return data.Select(d => DeviceData.CreateFromAdbData(d)).ToList();
            }
        }

        /// <inheritdoc/>
        public int CreateReverseForward(DeviceData device, string remote, string local, bool allowRebind)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                string rebind = allowRebind ? string.Empty : "norebind:";

                socket.SendAdbRequest($"reverse:forward:{rebind}{remote};{local}");
                AdbResponse response = socket.ReadAdbResponse();
                response = socket.ReadAdbResponse();
                string portString = socket.ReadString();

                return portString != null && int.TryParse(portString, out int port) ? port : 0;
            }
        }

        ///<inheritdoc/>
        public void RemoveReverseForward(DeviceData device, string remote)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                socket.SendAdbRequest($"reverse:killforward:{remote}");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        ///<inheritdoc/>
        public void RemoveAllReverseForwards(DeviceData device)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                socket.SendAdbRequest($"reverse:killforward-all");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <inheritdoc/>
        public int CreateForward(DeviceData device, string local, string remote, bool allowRebind)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                string rebind = allowRebind ? string.Empty : "norebind:";

                socket.SendAdbRequest($"host-serial:{device.Serial}:forward:{rebind}{local};{remote}");
                AdbResponse response = socket.ReadAdbResponse();
                response = socket.ReadAdbResponse();
                string portString = socket.ReadString();

                return portString != null && int.TryParse(portString, out int port) ? port : 0;
            }
        }

        /// <inheritdoc/>
        public int CreateForward(DeviceData device, ForwardSpec local, ForwardSpec remote, bool allowRebind)
        {
            return CreateForward(device, local?.ToString(), remote?.ToString(), allowRebind);
        }

        /// <inheritdoc/>
        public void RemoveForward(DeviceData device, int localPort)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host-serial:{device.Serial}:killforward:tcp:{localPort}");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <inheritdoc/>
        public void RemoveAllForwards(DeviceData device)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host-serial:{device.Serial}:killforward-all");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ForwardData> ListForward(DeviceData device)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host-serial:{device.Serial}:list-forward");
                AdbResponse response = socket.ReadAdbResponse();

                string data = socket.ReadString();

                string[] parts = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                return parts.Select(p => ForwardData.FromString(p));
            }
        }

        ///<inheritdoc/>
        public IEnumerable<ForwardData> ListReverseForward(DeviceData device)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                socket.SendAdbRequest($"reverse:list-forward");
                AdbResponse response = socket.ReadAdbResponse();

                string data = socket.ReadString();

                string[] parts = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                return parts.Select(p => ForwardData.FromString(p));
            }
        }

        /// <inheritdoc/>
        public Task ExecuteRemoteCommandAsync(string command, DeviceData device, IShellOutputReceiver receiver, CancellationToken cancellationToken)
        {
            return ExecuteRemoteCommandAsync(command, device, receiver, Encoding, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task ExecuteRemoteCommandAsync(string command, DeviceData device, IShellOutputReceiver receiver, Encoding encoding, CancellationToken cancellationToken)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                cancellationToken.Register(() => socket.Dispose());

                socket.SetDevice(device);
                socket.SendAdbRequest($"shell:{command}");
                AdbResponse response = socket.ReadAdbResponse();


                try
                {
                    using (StreamReader reader = new StreamReader(socket.GetShellStream(), encoding))
                    {
                        // Previously, we would loop while reader.Peek() >= 0. Turns out that this would
                        // break too soon in certain cases (about every 10 loops, so it appears to be a timing
                        // issue). Checking for reader.ReadLine() to return null appears to be much more robust
                        // -- one of the integration test fetches output 1000 times and found no truncations.
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            string line = await reader.ReadLineAsync().ConfigureAwait(false);

                            if (line == null)
                            {
                                break;
                            }

                            receiver?.AddOutput(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    // If a cancellation was requested, this main loop is interrupted with an exception
                    // because the socket is closed. In that case, we don't need to throw a ShellCommandUnresponsiveException.
                    // In all other cases, something went wrong, and we want to report it to the user.
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        throw new ShellCommandUnresponsiveException(e);
                    }
                }
                finally
                {
                    receiver?.Flush();
                }
            }
        }

        /// <inheritdoc/>
        public ShellSocket StartShell(DeviceData device)
        {
            EnsureDevice(device);

            IAdbSocket socket = adbSocketFactory(EndPoint);

            socket.SetDevice(device);
            socket.SendAdbRequest("shell:");
            AdbResponse response = socket.ReadAdbResponse();

            return new ShellSocket(socket);
        }

        /// <inheritdoc/>
        public Framebuffer CreateRefreshableFramebuffer(DeviceData device)
        {
            EnsureDevice(device);

            return new Framebuffer(device, this);
        }

        /// <inheritdoc/>
        public async Task<Image> GetFrameBufferAsync(DeviceData device, CancellationToken cancellationToken)
        {
            EnsureDevice(device);

            using (Framebuffer framebuffer = CreateRefreshableFramebuffer(device))
            {
                await framebuffer.RefreshAsync(cancellationToken).ConfigureAwait(false);

                // Convert the framebuffer to an image, and return that.
                return framebuffer.ToImage();
            }
        }

        /// <inheritdoc/>
        public async Task RunLogServiceAsync(DeviceData device, Action<LogEntry> messageSink, CancellationToken cancellationToken, params LogId[] logNames)
        {
            if (messageSink == null)
            {
                throw new ArgumentException(null, nameof(messageSink));
            }

            EnsureDevice(device);

            // The 'log' service has been deprecated, see
            // https://android.googlesource.com/platform/system/core/+/7aa39a7b199bb9803d3fd47246ee9530b4a96177
            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                StringBuilder request = new StringBuilder();
                request.Append("shell:logcat -B");

                foreach (LogId logName in logNames)
                {
                    request.Append($" -b {logName.ToString().ToLowerInvariant()}");
                }

                socket.SendAdbRequest(request.ToString());
                AdbResponse response = socket.ReadAdbResponse();

                using (Stream stream = socket.GetShellStream())
                {
                    LogReader reader = new LogReader(stream);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        LogEntry entry = null;

                        try
                        {
                            entry = await reader.ReadEntry(cancellationToken).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            // This indicates the end of the stream; the entry will remain null.
                        }

                        if (entry != null)
                        {
                            messageSink(entry);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Reboot(string into, DeviceData device)
        {
            EnsureDevice(device);

            string request = $"reboot:{into}";

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);
                socket.SendAdbRequest(request);
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <inheritdoc/>
        public void Connect(DnsEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host:connect:{endpoint.Host}:{endpoint.Port}");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <inheritdoc/>
        public void Pair(DnsEndPoint endpoint, int pairKey)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host:pair:{pairKey}:{endpoint.Host}:{endpoint.Port}");
                AdbResponse response = socket.ReadAdbResponse();
                string message = socket.ReadString();

                if (message.StartsWith("Failed:"))
                {
                    throw new AdbException(message);
                }
            }
        }

        /// <inheritdoc/>
        public void Root(DeviceData device)
        {
            Root("root:", device);
        }

        /// <inheritdoc/>
        public void Unroot(DeviceData device)
        {
            Root("unroot:", device);
        }

        /// <inheritdoc/>
        protected void Root(string request, DeviceData device)
        {
            EnsureDevice(device);

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);
                socket.SendAdbRequest(request);
                AdbResponse response = socket.ReadAdbResponse();

                // ADB will send some additional data
                byte[] buffer = new byte[1024];
                int read = socket.Read(buffer);

                string responseMessage = Encoding.UTF8.GetString(buffer, 0, read);

                // See https://android.googlesource.com/platform/system/core/+/master/adb/commandline.cpp#1026 (adb_root)
                // for more information on how upstream does this.
                if (!string.Equals(responseMessage, "restarting", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AdbException(responseMessage);
                }
                else
                {
                    // Give adbd some time to kill itself and come back up.
                    // We can't use wait-for-device because devices (e.g. adb over network) might not come back.
                    Task.Delay(3000).GetAwaiter().GetResult();
                }
            }
        }

        /// <inheritdoc/>
        public List<string> GetFeatureSet(DeviceData device)
        {
            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host-serial:{device.Serial}:features");

                AdbResponse response = socket.ReadAdbResponse();
                string features = socket.ReadString();

                List<string> featureList = features.Split(new char[] { '\n', ',' }).ToList();
                return featureList;
            }
        }

        /// <inheritdoc/>
        public void Install(DeviceData device, Stream apk, params string[] arguments)
        {
            EnsureDevice(device);

            if (apk == null)
            {
                throw new ArgumentNullException(nameof(apk));
            }

            if (!apk.CanRead || !apk.CanSeek)
            {
                throw new ArgumentOutOfRangeException(nameof(apk), "The apk stream must be a readable and seekable stream");
            }

            StringBuilder requestBuilder = new StringBuilder();
            requestBuilder.Append("exec:cmd package 'install' ");

            if (arguments != null)
            {
                foreach (string argument in arguments)
                {
                    requestBuilder.Append(" ");
                    requestBuilder.Append(argument);
                }
            }

            // add size parameter [required for streaming installs]
            // do last to override any user specified value
            requestBuilder.Append($" -S {apk.Length}");

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SetDevice(device);

                socket.SendAdbRequest(requestBuilder.ToString());
                AdbResponse response = socket.ReadAdbResponse();

                byte[] buffer = new byte[32 * 1024];
                int read = 0;

                while ((read = apk.Read(buffer, 0, buffer.Length)) > 0)
                {
                    socket.Send(buffer, read);
                }

                read = socket.Read(buffer);
                string value = Encoding.UTF8.GetString(buffer, 0, read);

                if (!string.Equals(value, "Success\n"))
                {
                    throw new AdbException(value);
                }
            }
        }

        ///<inheritdoc/>
        public void Disconnect(DnsEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            using (IAdbSocket socket = adbSocketFactory(EndPoint))
            {
                socket.SendAdbRequest($"host:disconnect:{endpoint.Host}:{endpoint.Port}");
                AdbResponse response = socket.ReadAdbResponse();
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the <paramref name="device"/>
        /// parameter is <see langword="null"/>, and a <see cref="ArgumentOutOfRangeException"/>
        /// if <paramref name="device"/> does not have a valid serial number.
        /// </summary>
        /// <param name="device">
        /// A <see cref="DeviceData"/> object to validate.
        /// </param>
        protected static void EnsureDevice(DeviceData device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            if (string.IsNullOrEmpty(device.Serial))
            {
                throw new ArgumentOutOfRangeException(nameof(device), "You must specific a serial number for the device");
            }
        }
    }
}