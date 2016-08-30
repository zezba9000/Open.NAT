using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Open.Nat.UnitTests
{
	[TestFixture]
	public class UpnpNatDeviceInfoTests
	{
		[Test]
		public void x()
		{
			var info = new UpnpNatDeviceInfo(IPAddress.Loopback, new Uri("http://127.0.0.1:3221"), "/control?WANIPConnection", null);
			Assert.AreEqual("http://127.0.0.1:3221/control?WANIPConnection", info.ServiceControlUri.ToString());
		}
	}
}
