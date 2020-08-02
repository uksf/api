using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace UKSF.Common {
    // Credit: https://stackoverflow.com/a/26372061/2516910
    public static class CertificateUtilities {
        [DllImport("Mssign32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SignerSign(
            IntPtr pSubjectInfo, // SIGNER_SUBJECT_INFO
            IntPtr pSignerCert, // SIGNER_CERT
            IntPtr pSignatureInfo, // SIGNER_SIGNATURE_INFO
            IntPtr pProviderInfo, // SIGNER_PROVIDER_INFO
            string pwszHttpTimeStamp, // LPCWSTR
            IntPtr psRequest, // PCRYPT_ATTRIBUTES
            IntPtr pSipData // LPVOID
        );

        [DllImport("Mssign32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SignerTimeStamp(
            IntPtr pSubjectInfo, // SIGNER_SUBJECT_INFO
            string pwszHttpTimeStamp, // LPCWSTR
            IntPtr psRequest, // PCRYPT_ATTRIBUTES
            IntPtr pSipData // LPVOID
        );

        [DllImport("Crypt32.dll", EntryPoint = "CertCreateCertificateContext", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr CertCreateCertificateContext(int dwCertEncodingType, byte[] pbCertEncoded, int cbCertEncoded);

        public static void SignWithThumbprint(string appPath, string thumbprint, string timestampUrl = "") {
            IntPtr pSignerCert = IntPtr.Zero;
            IntPtr pSubjectInfo = IntPtr.Zero;
            IntPtr pSignatureInfo = IntPtr.Zero;
            IntPtr pProviderInfo = IntPtr.Zero;

            try {
                pSignerCert = CreateSignerCert(thumbprint);
                pSubjectInfo = CreateSignerSubjectInfo(appPath);
                pSignatureInfo = CreateSignerSignatureInfo();

                SignCode(pSubjectInfo, pSignerCert, pSignatureInfo, pProviderInfo);

                if (!string.IsNullOrEmpty(timestampUrl)) {
                    TimeStampSignedCode(pSubjectInfo, timestampUrl);
                }
            } catch (CryptographicException ce) {
                throw new Exception($"An error occurred while attempting to load the signing certificate. {ce.Message}");
            } finally {
                if (pSignerCert != IntPtr.Zero) {
                    Marshal.DestroyStructure(pSignerCert, typeof(SignerCert));
                }

                if (pSubjectInfo != IntPtr.Zero) {
                    Marshal.DestroyStructure(pSubjectInfo, typeof(SignerSubjectInfo));
                }

                if (pSignatureInfo != IntPtr.Zero) {
                    Marshal.DestroyStructure(pSignatureInfo, typeof(SignerSignatureInfo));
                }
            }
        }

        private static IntPtr CreateSignerSubjectInfo(string pathToAssembly) {
            SignerSubjectInfo info = new SignerSubjectInfo { cbSize = (uint) Marshal.SizeOf(typeof(SignerSubjectInfo)), pdwIndex = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(uint))) };
            int index = 0;
            Marshal.StructureToPtr(index, info.pdwIndex, false);

            info.dwSubjectChoice = 0x1; //SIGNER_SUBJECT_FILE
            IntPtr assemblyFilePtr = Marshal.StringToHGlobalUni(pathToAssembly);

            SignerFileInfo fileInfo = new SignerFileInfo { cbSize = (uint) Marshal.SizeOf(typeof(SignerFileInfo)), pwszFileName = assemblyFilePtr, hFile = IntPtr.Zero };

            info.Union1 = new SignerSubjectInfo.SubjectChoiceUnion { pSignerFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SignerFileInfo))) };

            Marshal.StructureToPtr(fileInfo, info.Union1.pSignerFileInfo, false);

            IntPtr pSubjectInfo = Marshal.AllocHGlobal(Marshal.SizeOf(info));
            Marshal.StructureToPtr(info, pSubjectInfo, false);

            return pSubjectInfo;
        }

        private static X509Certificate2 FindCertByThumbprint(string thumbprint) {
            try {
                string thumbprintFixed = thumbprint.Replace(" ", string.Empty).ToUpperInvariant();

                X509Store[] stores = {
                    new X509Store(StoreName.My, StoreLocation.CurrentUser),
                    new X509Store(StoreName.My, StoreLocation.LocalMachine),
                    new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser),
                    new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine)
                };

                foreach (X509Store store in stores) {
                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprintFixed, false);
                    store.Close();

                    if (certs.Count < 1) {
                        continue;
                    }

                    return certs[0];
                }

                throw new Exception($"A certificate matching the thumbprint: {thumbprint} could not be found.  Make sure that a valid certificate matching the provided thumbprint is installed.");
            } catch (Exception e) {
                throw new Exception($"{e.Message}");
            }
        }

        private static IntPtr CreateSignerCert(string thumbprint) {
            SignerCert signerCert = new SignerCert {
                cbSize = (uint) Marshal.SizeOf(typeof(SignerCert)),
                dwCertChoice = 0x2,
                Union1 = new SignerCert.SignerCertUnion { pCertStoreInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SignerCertStoreInfo))) },
                hwnd = IntPtr.Zero
            };

            const int X509_ASN_ENCODING = 0x00000001;
            const int PKCS_7_ASN_ENCODING = 0x00010000;

            X509Certificate2 cert = FindCertByThumbprint(thumbprint);

            IntPtr pCertContext = CertCreateCertificateContext(X509_ASN_ENCODING | PKCS_7_ASN_ENCODING, cert.GetRawCertData(), cert.GetRawCertData().Length);

            SignerCertStoreInfo certStoreInfo = new SignerCertStoreInfo {
                cbSize = (uint) Marshal.SizeOf(typeof(SignerCertStoreInfo)),
                pSigningCert = pCertContext,
                dwCertPolicy = 0x2, // SIGNER_CERT_POLICY_CHAIN
                hCertStore = IntPtr.Zero
            };

            Marshal.StructureToPtr(certStoreInfo, signerCert.Union1.pCertStoreInfo, false);

            IntPtr pSignerCert = Marshal.AllocHGlobal(Marshal.SizeOf(signerCert));
            Marshal.StructureToPtr(signerCert, pSignerCert, false);

            return pSignerCert;
        }

        private static IntPtr CreateSignerSignatureInfo() {
            SignerSignatureInfo signatureInfo = new SignerSignatureInfo {
                cbSize = (uint) Marshal.SizeOf(typeof(SignerSignatureInfo)),
                algidHash = 0x00008004, // CALG_SHA1
                dwAttrChoice = 0x0, // SIGNER_NO_ATTR
                pAttrAuthCode = IntPtr.Zero,
                psAuthenticated = IntPtr.Zero,
                psUnauthenticated = IntPtr.Zero
            };

            IntPtr pSignatureInfo = Marshal.AllocHGlobal(Marshal.SizeOf(signatureInfo));
            Marshal.StructureToPtr(signatureInfo, pSignatureInfo, false);

            return pSignatureInfo;
        }

        private static void SignCode(IntPtr pSubjectInfo, IntPtr pSignerCert, IntPtr pSignatureInfo, IntPtr pProviderInfo) {
            int hResult = SignerSign(pSubjectInfo, pSignerCert, pSignatureInfo, pProviderInfo, null, IntPtr.Zero, IntPtr.Zero);

            if (hResult != 0) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        private static void TimeStampSignedCode(IntPtr pSubjectInfo, string timestampUrl) {
            int hResult = SignerTimeStamp(pSubjectInfo, timestampUrl, IntPtr.Zero, IntPtr.Zero);

            if (hResult != 0) {
                throw new Exception($"{timestampUrl} could not be used at this time. If necessary, check the timestampUrl, internet connection, and try again.");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignerSubjectInfo {
            public uint cbSize;
            public IntPtr pdwIndex;
            public uint dwSubjectChoice;
            public SubjectChoiceUnion Union1;

            [StructLayoutAttribute(LayoutKind.Explicit)]
            internal struct SubjectChoiceUnion {
                [FieldOffsetAttribute(0)] public IntPtr pSignerFileInfo;
                [FieldOffsetAttribute(0)] public readonly IntPtr pSignerBlobInfo;
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SignerCert {
            public uint cbSize;
            public uint dwCertChoice;
            public SignerCertUnion Union1;

            [StructLayoutAttribute(LayoutKind.Explicit)]
            internal struct SignerCertUnion {
                [FieldOffsetAttribute(0)] public readonly IntPtr pwszSpcFile;
                [FieldOffsetAttribute(0)] public IntPtr pCertStoreInfo;
                [FieldOffsetAttribute(0)] public readonly IntPtr pSpcChainInfo;
            }

            public IntPtr hwnd;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SignerSignatureInfo {
            public uint cbSize;
            public uint algidHash; // ALG_ID
            public uint dwAttrChoice;
            public IntPtr pAttrAuthCode;
            public IntPtr psAuthenticated; // PCRYPT_ATTRIBUTES
            public IntPtr psUnauthenticated; // PCRYPT_ATTRIBUTES
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SignerFileInfo {
            public uint cbSize;
            public IntPtr pwszFileName;
            public IntPtr hFile;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SignerCertStoreInfo {
            public uint cbSize;
            public IntPtr pSigningCert; // CERT_CONTEXT
            public uint dwCertPolicy;
            public IntPtr hCertStore;
        }
    }
}
