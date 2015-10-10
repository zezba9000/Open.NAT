using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.Nat
{
    /// <summary>
    /// 
    /// </summary>
    public class NatDiscoverer
    {
        /// <summary>
        /// The <see cref="http://msdn.microsoft.com/en-us/library/vstudio/system.diagnostics.tracesource">TraceSource</see> instance
        /// used for debugging and <see cref="https://github.com/lontivero/Open.Nat/wiki/Troubleshooting">Troubleshooting</see>
        /// </summary>
        /// <example>
        /// NatUtility.TraceSource.Switch.Level = SourceLevels.Verbose;
        /// NatUtility.TraceSource.Listeners.Add(new ConsoleListener());
        /// </example>
        /// <remarks>
        /// At least one trace listener has to be added to the Listeners collection if a trace is required; if no listener is added
        /// there will no be tracing to analyse.
        /// </remarks>
        /// <remarks>
        /// Open.NAT only supports SourceLevels.Verbose, SourceLevels.Error, SourceLevels.Warning and SourceLevels.Information.
        /// </remarks>
        public readonly static TraceSource TraceSource = new TraceSource("Open.NAT");

		private static readonly Dictionary<string, UpnpNatDevice> Devices = new Dictionary<string, UpnpNatDevice>();

        // Finalizer is never used however its destructor, that releases the open ports, is invoked by the
        // process as part of the shuting down step. So, don't remove it!
        private static readonly Finalizer Finalizer = new Finalizer();
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 5000, 2000);

        /// <summary>
        /// Discovers and returns an UPnp or Pmp NAT device; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see>
        /// exception is thrown after 3 seconds. 
        /// </summary>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before 3 seconds.</exception>
		public async Task<UpnpNatDevice> DiscoverDeviceAsync()
        {
            var cts = new CancellationTokenSource(3 * 1000);
            return await DiscoverDeviceAsync(cts);
        }

        /// <summary>
        /// Discovers and returns a NAT device for the specified type; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see> 
        /// exception is thrown when it is cancelled. 
        /// </summary>
        /// <remarks>
        /// It allows to specify the NAT type to discover as well as the cancellation token in order.
        /// </remarks>
        /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before cancellation</exception>
		public async Task<UpnpNatDevice> DiscoverDeviceAsync(CancellationTokenSource cancellationTokenSource)
        {
            Guard.IsNotNull(cancellationTokenSource, "cancellationTokenSource");

            var devices = await DiscoverAsync(true, cancellationTokenSource);
            var device = devices.FirstOrDefault();
            if(device==null)
            {
                TraceSource.LogInfo("Device not found. Common reasons:");
                TraceSource.LogInfo("\t* No device is present or,");
                TraceSource.LogInfo("\t* Upnp is disabled in the router or");
                TraceSource.LogInfo("\t* Antivirus software is filtering SSDP (discovery protocol).");
                throw new NatDeviceNotFoundException();
            }
            return device;
        }

        /// <summary>
        /// Discovers and returns all NAT devices for the specified type. If no NAT device is found it returns an empty enumerable
        /// </summary>
        /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
        /// <returns>All found NAT devices</returns>
		public async Task<IEnumerable<UpnpNatDevice>> DiscoverDevicesAsync(CancellationTokenSource cancellationTokenSource)
        {
            Guard.IsNotNull(cancellationTokenSource, "cancellationTokenSource");

            var devices = await DiscoverAsync(false, cancellationTokenSource);
            return devices.ToArray();
        }

		private async Task<IEnumerable<UpnpNatDevice>> DiscoverAsync(bool onlyOne, CancellationTokenSource cts)
        {
            TraceSource.LogInfo("Start Discovery");
            var upnpSearcher = new UpnpSearcher(new IPAddressesProvider());
            upnpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
            var devices = await upnpSearcher.Search(cts.Token);

            TraceSource.LogInfo("Stop Discovery");
            
            foreach (var device in devices)
            {
                var key = device.ToString();
                UpnpNatDevice nat;
                if(!Devices.TryGetValue(key, out nat))
                {
                    Devices.Add(key, device.Value);
                }
            }
            return devices.Values;
        }

        /// <summary>
        /// Release all ports opened by Open.NAT. 
        /// </summary>
        /// <remarks>
        /// If ReleaseOnShutdown value is true, it release all the mappings created through the library.
        /// </remarks>
        public static void ReleaseAll()
        {
            foreach (var device in Devices.Values)
            {
                device.ReleaseAll();
            }
        }

        internal static void ReleaseSessionMappings()
        {
            foreach (var device in Devices.Values)
            {
                device.ReleaseSessionMappings();
            }
        }

        private static void RenewMappings(object state)
        {
            Task.Factory.StartNew(async ()=> 
            {
                foreach (var device in Devices.Values)
                {
                    await device.RenewMappings();
                }
            });
        }
    }
}