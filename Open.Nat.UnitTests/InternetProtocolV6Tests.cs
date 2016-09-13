using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Open.Nat.Tests
{
	[TestFixture]
	public class InternetProtocolV6Tests
	{
		private UpnpMockServer _server;
		private ServerConfiguration _cfg;

		[SetUp]
		public void Setup()
		{
			_cfg = new ServerConfiguration();
			_cfg.Prefix = "http://*:5431/";
			_cfg.ServiceUrl = "http://[::1]:5431/dyndev/uuid:0000e068-20a0-00e0-20a0-48a8000808e0";
			_cfg.ControlUrl = "http://[::1]:5431/uuid:0000e068-20a0-00e0-20a0-48a802086048/WANIPConnection:1";
			_server = new UpnpMockServer(_cfg);
			_server.Start();
		}

		[TearDown]
		public void TearDown()
		{
			_server.Dispose();
		}

		[Test]
		public async Task Connect()
		{
			var nat = new NatDiscoverer();
			var cts = new CancellationTokenSource(5000);
			_server.WhenDiscoveryRequest = () =>
					  "HTTP/1.1 200 OK\r\n"
					+ "Server: Custom/1.0 UPnP/1.0 Proc/Ver\r\n"
					+ "EXT:\r\n"
					+ "Location: http://[::1]:5431/dyndev/uuid:0000e068-20a0-00e0-20a0-48a8000808e0\r\n"
					+ "Cache-Control:max-age=1800\r\n"
					+ "ST:urn:schemas-upnp-org:service:WANIPConnection:1\r\n"
					+ "USN:uuid:0000e068-20a0-00e0-20a0-48a802086048::urn:schemas-upnp-org:service:WANIPConnection:1";

			_server.WhenGetExternalIpAddress = (ctx) =>
			{
				var responseXml = "<?xml version=\"1.0\"?>" +
					"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
					"s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
					"<s:Body>" +
					"<m:GetExternalIPAddressResponse xmlns:m=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
					"<NewExternalIPAddress>FE80::0202:B3FF:FE1E:8329</NewExternalIPAddress>" +
					"</m:GetExternalIPAddressResponse>" +
					"</s:Body>" +
					"</s:Envelope>";
				var bytes = Encoding.UTF8.GetBytes(responseXml);
				var response = ctx.Response;
				response.OutputStream.Write(bytes, 0, bytes.Length);
				response.OutputStream.Flush();
				response.StatusCode = 200;
				response.StatusDescription = "OK";
				response.Close();
			};

			var device = await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts);
			Assert.NotNull(device);

			var ip = await device.GetExternalIPAsync();
			Assert.AreEqual(IPAddress.Parse("FE80::0202:B3FF:FE1E:8329"), ip);
		}
	}
}
