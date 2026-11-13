using EEBUS.Models;
using EEBUS.Net.Events;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS.Net.Connections;

/// <summary>
/// Decides whether a remote device may communicate with the local device: the set of trusted SKIs, certificate checks
/// (SKI / SHIP pairing fingerprint) for incoming and outgoing connections, and whether new SHIP pairing requests may be evaluated.
/// </summary>
internal sealed class TrustStore(Devices devices, Settings settings, ILogger? logger)
{
    private static readonly TimeSpan PairingReleaseTimeout = TimeSpan.FromMinutes(15);

    private readonly HashSet<string> _trustedSkis = [];
    private readonly Lock _lock = new();

    public void Trust(string ski)
    {
        lock (_lock)
            _trustedSkis.Add(ski);
    }

    public void Distrust(string ski)
    {
        lock (_lock)
            _trustedSkis.Remove(ski);
    }

    public bool IsTrusted(string ski)
    {
        lock (_lock)
            return _trustedSkis.Contains(ski);
    }

    internal List<string> GetTrustedSkis()
    {
        lock (_lock)
            return _trustedSkis.ToList();
    }

    /// <summary>
    /// Validates the certificate presented by a remote device we connect to as a client.
    /// </summary>
    public bool ValidateServerCertificate(RemoteDevice device, X509Certificate? cert)
    {
        if (cert == null)
            return false;

        var pairedDevices = devices.GetPairedDevices();

        //strict mode (only ship paired devices can communicate):
        if (settings.UseStrictShipPairing && pairedDevices.Any(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.None))
            return false;

        var paired = pairedDevices.FirstOrDefault(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.AddCu);
        /*
        TODO: we imagine the following scenario:
        1. devZ pairs via ship pairing
        2. devZ disconnects for more than 15 minutes
        3. a new devZ2 pairs via ship pairing
        4. devZ comes back online and attempts a connection
        Normally, this would not be possible because according to spec, devZ is not trusted anymore. However, with our system, if the device is still teached in, its also still trusted. We would need
        to automatically delete/disable/set offline the device. Also for the pairing, according to spec, after the device is paired its considered trusted, which would mean we would need to automatically teach it in
         */
        bool certificateValid = paired == null     //device was never paired via ship pairing -> ski verification
            ? CertificateMatchesSki(device, cert)
            : CertificateMatchesFingerprint(device, cert, paired.TrustPar);

        return certificateValid && IsTrusted(device.SKI.ToString());
    }

    /// <summary>
    /// Validates an incoming connection accepted by the SHIP listener.
    /// </summary>
    public bool ValidateIncomingConnection(NewConnectionValidationEventArgs args)
    {
        RemoteDevice? device = devices.GetRemotes().FirstOrDefault(rd => rd.SKI.ToString() == args.Ski);
        if (device == null)
        {
            logger?.LogError("remote device with SKI {Ski} has no mDNS advertisements", args.Ski);
            return false;
        }

        var paired = devices.GetPairedDevices().FirstOrDefault(p => p.TrustId == device.Id);

        bool certificateValid = paired != null //device was added via ship pairing
            ? CertificateMatchesFingerprint(device, args.Certificate, paired.TrustPar)
            : CertificateMatchesSki(device, args.Certificate);

        return certificateValid && IsTrusted(args.Ski);
    }

    /// <summary>
    /// SHIP pairing requests are only evaluated if no device is paired yet, or the paired device has been offline long enough.
    /// </summary>
    public bool CanEvaluateShipPairingRequests(Func<string, bool> isConnected)
    {
        var pairedAddCu = devices.GetPairedDevices().FirstOrDefault(p => p.TrustType == EEBUS.Models.ShipTrustType.AddCu);
        if (pairedAddCu == null) return true;  //no device yet paired via ship pairing, so we can accept new requests

        var remote = devices.GetRemote(pairedAddCu.TrustId);   //the remote device corresponding to the paired device
        if (remote == null || remote.LastDisconnectUtc == null) return false;
        if (isConnected(remote.SKI.ToString())) return false;

        return DateTime.UtcNow - remote.LastDisconnectUtc.Value > PairingReleaseTimeout;
    }

    private bool CertificateMatchesSki(RemoteDevice device, X509Certificate cert)
    {
        string certSki = new SKI(SHA1.HashData(cert.GetPublicKey() ?? [])).ToString();
        string deviceSki = device.SKI.ToString();
        if (certSki == deviceSki)
            return true;

        logger?.LogError("Certificate SKI {certSki} does not match device SKI {deviceSki}", certSki, deviceSki);
        return false;
    }

    private bool CertificateMatchesFingerprint(RemoteDevice device, X509Certificate cert, string? trustPar)
    {
        if (Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())) == trustPar)
            return true;

        logger?.LogError("Certificate fingerprint for id {deviceId} does not match persisted trustPar", device.Id);
        return false;
    }
}
