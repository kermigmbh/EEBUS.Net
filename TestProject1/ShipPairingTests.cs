using EEBUS;
using EEBUS.Models;
using Makaretu.Dns;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TestProject1
{
    public class ShipPairingTests : EebusTests
    {
        [Fact]
        public void ShipPairing_CalculatesDigestCorrectly()
        {
            //Values taken from Ship pairing specification, Annex A
            List<string> txtRecords = new()
            {
                "txtvers=1",
                "parType=fpSha256",
                "forId=i:983327_u:C8277H008F-3",
                "forPar=C74B7855D3479415F62CC01E5F6D9A93EBC676057D85417ADA16FD1384338943",
                "trustId=i:46925_u:43652bk-2-gt1",
                "trustPar=2CC72E781F7A7D2A08D50196C50FEDF0F7BA583F43F76C8C0DDEC9EEF0D005B4",
                "trustCurve=secp256r1",
                "type=addCu",
                "trustNonce=BDCEE427FA7208DF3C1F2A749BA6F4D4",
                "alg=hmacSha256",
            };

            Assert.True(ValidateDigest("hmacSha256", "BDCEE427FA7208DF3C1F2A749BA6F4D4", "BCBB62B2176DA2CEE545784CEB1F2A55E049451B12A549C98E8CA213F001DA25", "7A37DCF81BDB50F8E92CFA4160CCB3DE", txtRecords));
        }

        private bool ValidateDigest(string algorithm, string trustNonce, string digest, string secret, IEnumerable<string> txtRecords)
        {
            string key = secret + trustNonce;

            StringBuilder sb = new StringBuilder();
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("txtvers"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("parType"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("forId"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("forPar"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustId"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustPar"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustCurve"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("type"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustNonce"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("alg"))};");

            string message = sb.ToString();
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] keyBytes = Convert.FromHexString(key);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(messageBytes);
            string calculatedDigest = Convert.ToHexString(hash);

            return calculatedDigest == digest;
        }
    }
}
