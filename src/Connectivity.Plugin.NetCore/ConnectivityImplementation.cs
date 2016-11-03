using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Connectivity.Abstractions;
using System.Net.NetworkInformation;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;

namespace Plugin.Connectivity
{

    public class ConnectivityImplementation
        : BaseConnectivity
    {

        #region Fields
        bool isConnected;
        #endregion

        #region Properties
        /// <summary>
        /// Gets if there is an active internet connection
        /// </summary>
        public override bool IsConnected
        {
            get
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
        }

        /// <summary>
        /// Gets the list of all active connection types.
        /// </summary>
        public override IEnumerable<ConnectionType> ConnectionTypes
        {
            get
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var item in interfaces)
                {
                    switch (item.NetworkInterfaceType)
                    {
                        case NetworkInterfaceType.Wireless80211:
                            yield return ConnectionType.WiFi;
                            break;
                        case NetworkInterfaceType.Ppp:
                        case NetworkInterfaceType.AsymmetricDsl:
                        case NetworkInterfaceType.Atm:
                        case NetworkInterfaceType.BasicIsdn:
                        case NetworkInterfaceType.Ethernet:
                        case NetworkInterfaceType.Ethernet3Megabit:
                        case NetworkInterfaceType.FastEthernetFx:
                        case NetworkInterfaceType.FastEthernetT:
                        case NetworkInterfaceType.Fddi:
                        case NetworkInterfaceType.GenericModem:
                        case NetworkInterfaceType.GigabitEthernet:
                        case NetworkInterfaceType.HighPerformanceSerialBus:
                        case NetworkInterfaceType.IPOverAtm:
                        case NetworkInterfaceType.Isdn:
                        case NetworkInterfaceType.MultiRateSymmetricDsl:
                        case NetworkInterfaceType.PrimaryIsdn:
                        case NetworkInterfaceType.RateAdaptDsl:
                        case NetworkInterfaceType.Slip:
                        case NetworkInterfaceType.SymmetricDsl:
                        case NetworkInterfaceType.TokenRing:
                        case NetworkInterfaceType.Tunnel:
                        case NetworkInterfaceType.Unknown:
                        case NetworkInterfaceType.VeryHighSpeedDsl:
                        case NetworkInterfaceType.Wman:
                        case NetworkInterfaceType.Wwanpp:
                        case NetworkInterfaceType.Wwanpp2:
                            yield return ConnectionType.Desktop;
                            break;
                        case NetworkInterfaceType.Loopback:
                        default:
                            yield return ConnectionType.Other;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves a list of available bandwidths for the platform.
        /// Only active connections.
        /// </summary>
        public override IEnumerable<UInt64> Bandwidths
        {
            get
            {
                var networkInterfaceList = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var networkInterfaceInfo in networkInterfaceList.Where(networkInterfaceInfo => networkInterfaceInfo.OperationalStatus == OperationalStatus.Up))
                {
                    yield return (UInt64)networkInterfaceInfo.Speed;
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public ConnectivityImplementation()
        {
            this.isConnected = IsConnected;
            NetworkChange.NetworkAddressChanged += NetworkStatusChanged;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Checks if remote is reachable.
        /// You can use it to check remote calls though.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="msTimeout"></param>
        /// <returns></returns>
        public override async Task<bool> IsReachable(string host, int msTimeout = 5000)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException("host");

            if (!IsConnected)
                return false;

            try
            {
                var task = Dns.GetHostAddressesAsync(host);
                if (!task.Wait(msTimeout))
                {
                    return false;
                }
                if (task.Result?.Any() ?? false)
                {
                    foreach (var ip in task.Result)
                    {
                        var ipAddr = ip.ToString();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to reach: " + host + " Error: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Tests if a remote host name is reachable
        /// </summary>
        /// <param name="host">Host name can be a remote IP or URL of website</param>
        /// <param name="port">Port to attempt to check is reachable.</param>
        /// <param name="msTimeout">Timeout in milliseconds.</param>
        /// <returns></returns>
        public override async Task<bool> IsRemoteReachable(string host, int port = 80, int msTimeout = 5000)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException("host");

            if (!IsConnected)
                return false;

            host = host.Replace("http://www.", string.Empty).
              Replace("http://", string.Empty).
              Replace("https://www.", string.Empty).
              Replace("https://", string.Empty).
              TrimEnd('/');

            return await Task.Run(() =>
            {
                try
                {
                    var clientDone = new ManualResetEvent(false);
                    var reachable = false;
                    var hostEntry = new DnsEndPoint(host, port);
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        var socketEventArg = new SocketAsyncEventArgs { RemoteEndPoint = hostEntry };
                        socketEventArg.Completed += (s, e) =>
                        {
                            reachable = e.SocketError == SocketError.Success;
                            clientDone.Set();
                        };

                        clientDone.Reset();

                        socket.ConnectAsync(socketEventArg);

                        clientDone.WaitOne(msTimeout);

                        return reachable;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Unable to reach: " + host + " Error: " + ex);
                    return false;
                }
            });
        }

        private void NetworkStatusChanged(object sender, EventArgs e)
        {
            var previous = isConnected;
            var newConnected = IsConnected;
            if (previous != newConnected)
            {
                OnConnectivityChanged(new ConnectivityChangedEventArgs { IsConnected = newConnected });
            }
        }
        #endregion

        #region IDisposable
        private bool disposed = false;
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        public override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    NetworkChange.NetworkAddressChanged -= NetworkStatusChanged;
                }

                disposed = true;
            }

            base.Dispose(disposing);
        }
        #endregion

    }

}