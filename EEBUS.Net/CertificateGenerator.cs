using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS
{
	public class CertificateGenerator
	{
		private static object Mutex = new();

		public static X509Certificate2 GenerateCert(IFileSystem fileSystem, string basePath, string subject )
		{
			ArgumentNullException.ThrowIfNull( fileSystem );

			lock ( Mutex )
			{
				if (string.IsNullOrEmpty(basePath))
				{
					basePath = fileSystem.Directory.GetCurrentDirectory();
				}
				string path		= fileSystem.Path.Combine(basePath, subject + ".pfx" );
				string password = "xcenter";

				// check if we have a persisted cert already
				if ( fileSystem.File.Exists( path ) )
				{
					// load and return
					return LoadCert( fileSystem.File.ReadAllBytes( path ), password );
				}
				else
				{
					try
					{
						string rootPath = fileSystem.Path.Combine(basePath, subject + ".pkcs12" );
						X509Certificate2 rootCertificate;
						if ( fileSystem.File.Exists( rootPath ) )
						{
							rootCertificate = X509CertificateLoader.LoadPkcs12( fileSystem.File.ReadAllBytes( rootPath ), "xcenterRoot" );
						}
						else
						{
							rootCertificate = CreateRootCertificate();
							fileSystem.File.WriteAllBytes( rootPath, rootCertificate.Export( X509ContentType.Pfx, "xcenterRoot" ) );
						}

						using ( rootCertificate )
						using ( ECDsa ecdsa = ECDsa.Create( ECCurve.NamedCurves.nistP256 ) )
						{
							// generate a new cert request with ECC and NIST curve P256 (TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256, EEBUS requirement)
							CertificateRequest request = new CertificateRequest(
								//"cn=" + subject,
								"CN=localhost",
								ecdsa,
								HashAlgorithmName.SHA256 );

							// add Subject Key Identifier (EEBUS requirement)
							request.CertificateExtensions.Add( new X509SubjectKeyIdentifierExtension( request.PublicKey, false ) );

							// add DNS name and localhost IP address also as Subject Alternate Name
							SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
							subjectAlternativeNameBuilder.AddDnsName( subject );
							request.CertificateExtensions.Add( subjectAlternativeNameBuilder.Build() );

							// add key usage
							request.CertificateExtensions.Add( new X509KeyUsageExtension(
								X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature,
								true ) );

							request.CertificateExtensions.Add( new X509EnhancedKeyUsageExtension(
								new OidCollection
								{
									new Oid( "1.3.6.1.5.5.7.3.1" ), // Server Authentication
									new Oid( "1.3.6.1.5.5.7.3.2" )  // Client Authentication
								},
								true ) );

							// create cert
							X509Certificate2 cert = request.Create(
								rootCertificate,
								DateTimeOffset.UtcNow.AddSeconds( 5 ),
								DateTimeOffset.UtcNow.AddYears( 9 ),
								Guid.NewGuid().ToByteArray() );

							byte[] pfx;
							using ( cert )
							using ( X509Certificate2 certWithKey = cert.CopyWithPrivateKey( ecdsa ) )
							{
								pfx = certWithKey.Export( X509ContentType.Pfx, password );
								// persist the cert
								fileSystem.File.WriteAllBytes( path, pfx );

								ExtractCertFile( fileSystem, certWithKey, basePath, subject );	// needed by the vue frontend
								ExtractKeyFile( fileSystem, certWithKey, basePath, subject );
							}

							// Windows SChannel cannot use the ephemeral key from CopyWithPrivateKey for TLS (0x8009030D).
							// Re-importing the PFX puts the key in a temporary container (no PersistKeySet = not kept in the user store).
							// Not required on Linux (OpenSSL), but harmless.
							return LoadCert( pfx, password );
						}
					}
					catch ( Exception ex )
					{
						Debug.WriteLine( "Cert generation error: " + ex.Message );
						throw;
					}
				}
			}
		}

		// No PersistKeySet: the private key only lives in a temporary key container that is removed when the certificate is disposed.
		private static X509Certificate2 LoadCert( byte[] pfx, string password )
			=> X509CertificateLoader.LoadPkcs12( pfx, password, X509KeyStorageFlags.DefaultKeySet );

		private static X509Certificate2 CreateRootCertificate()
		{
			using ( ECDsa ecdsa = ECDsa.Create( ECCurve.NamedCurves.nistP256 ))
			{
				CertificateRequest request = new CertificateRequest(
					"CN=XCenter Root Certificate",
					ecdsa,
					HashAlgorithmName.SHA256 );

				// Setze die Extensions für das Root-Zertifikat
				request.CertificateExtensions.Add(
					new X509BasicConstraintsExtension( true, false, 0, true ) ); // Kennzeichnung als Root-Zertifikat
				request.CertificateExtensions.Add(
					new X509KeyUsageExtension(
						X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
						true ) ); // Zertifikat darf andere signieren
				request.CertificateExtensions.Add(
					new X509SubjectKeyIdentifierExtension( request.PublicKey, false ) );

				// Erzeuge ein selbstsigniertes Zertifikat (10 Jahre gültig)
				return request.CreateSelfSigned(
					DateTimeOffset.Now,
					DateTimeOffset.Now.AddYears( 10 ) );
			}
		}

		private static void ExtractCertFile( IFileSystem fileSystem, X509Certificate2 certificate, string dir, string subject )
		{
			string pemPath = fileSystem.Path.Combine( dir, subject + ".pem" );

			// Speichern des Zertifikats (Public Key)
			var publicKeyPem = "-----BEGIN CERTIFICATE-----\n" +
							   Convert.ToBase64String( certificate.RawData, Base64FormattingOptions.InsertLineBreaks ) +
							   "\n-----END CERTIFICATE-----";
				fileSystem.File.WriteAllText( pemPath, publicKeyPem );
		}

		private static void ExtractKeyFile( IFileSystem fileSystem, X509Certificate2 certificate, string dir, string subject )
		{
			string keyPath = fileSystem.Path.Combine( dir, subject + ".key" );

			// Speichern des privaten Schlüssels
			var ecdsa = certificate.GetECDsaPrivateKey();
			if ( ecdsa != null )
			{
				var privateKeyBytes = ecdsa.ExportPkcs8PrivateKey();
				var privateKeyPem = "-----BEGIN PRIVATE KEY-----\n" +
									Convert.ToBase64String( privateKeyBytes, Base64FormattingOptions.InsertLineBreaks ) +
									"\n-----END PRIVATE KEY-----";
				fileSystem.File.WriteAllText( keyPath, privateKeyPem );
			}
		}
	}
}
