﻿using Fiddler;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace FortniteBurger.Classes
{
    internal class FiddlerCore
    {
        public static bool FiddlerIsRunning = false;
        internal static SessionStateHandler GrabWithShutdown = new SessionStateHandler(CookieGrabWithShutdown);
        internal static SessionStateHandler GrabWithoutShutdown = new SessionStateHandler(CookieGrabWithoutShutdown);
        internal static SessionStateHandler LaunchedWithProfileEditor = new SessionStateHandler(ProfileEditor);

        internal FiddlerCore()
        {
            EnsureRootCertGrabber();
            EnsureRootCertificate();
        }

        private static void EnsureRootCertGrabber()
        {
            CertMaker.createRootCert();
            string str = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cookie-Grabber");
            if (!Directory.Exists(str))
                Directory.CreateDirectory(str);
            string path = Path.Combine(str, "root.cer");
            X509Certificate2 rootCertificate = CertMaker.GetRootCertificate();
            rootCertificate.FriendlyName = "Cookie Grabber";
            File.WriteAllBytes(path, rootCertificate.Export(X509ContentType.Cert));
            X509Store x509Store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            x509Store.Open(OpenFlags.ReadWrite);
            x509Store.Add(rootCertificate);
            x509Store.Close();
        }

        private static void EnsureRootCertificate()
        {
            BCCertMaker.BCCertMaker bcCertMaker = new BCCertMaker.BCCertMaker();
            CertMaker.oCertProvider = bcCertMaker;
            string str = Path.Combine(Path.GetTempPath(), "defaultCertificate.p12");
            string password = "$0M3$H1T";
            if (!File.Exists(str))
            {
                CertMaker.removeFiddlerGeneratedCerts(true);
                bcCertMaker.CreateRootCertificate();
                bcCertMaker.WriteRootCertificateAndPrivateKeyToPkcs12File(str, password, null);
            }
            else
                bcCertMaker.ReadRootCertificateAndPrivateKeyFromPkcs12File(str, password, null);
            if (CertMaker.rootCertIsTrusted())
                return;
            CertMaker.trustRootCert();
        }

        public static void StartFiddlerCore()
        {
            EnsureRootCertGrabber();
            EnsureRootCertificate();
            FiddlerIsRunning = true;
            CONFIG.IgnoreServerCertErrors = true;
            FiddlerApplication.Startup(new FiddlerCoreStartupSettingsBuilder().ListenOnPort((ushort)8888).DecryptSSL().OptimizeThreadPool().ChainToUpstreamGateway().RegisterAsSystemProxy().Build());
        }

        public static void StartWithShutdown()
        {
            FiddlerApplication.BeforeRequest += GrabWithShutdown;
        }

        public static void StartWithoutShutdown()
        {
            FiddlerApplication.BeforeRequest -= GrabWithoutShutdown; // Vær sikker på at den ikke er registreret til eventet 2 gange
            FiddlerApplication.BeforeRequest += GrabWithoutShutdown;
        }

        public static void LaunchProfileEditor()
        {
            FiddlerApplication.BeforeRequest -= LaunchedWithProfileEditor;
            FiddlerApplication.BeforeRequest += LaunchedWithProfileEditor;
        }

        public static void StopFiddlerCore()
        {
            FiddlerApplication.BeforeRequest -= LaunchedWithProfileEditor;
            FiddlerApplication.BeforeRequest -= GrabWithoutShutdown;

            FiddlerApplication.Shutdown();
            FiddlerIsRunning = false;
        }

        private static void ProfileEditor(Session oSession)
        {
            if ((oSession).uriContains("/api/v1/dbd-character-data/get-all") && MainWindow.profile.FullProfile && !MainWindow.profile.Off) 
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Profile.json";

            if ((oSession).uriContains("/api/v1/dbd-character-data/bloodweb") && MainWindow.profile.FullProfile && !MainWindow.profile.Off)
            {
                //Utils.UpdatedBloodweb((oSession).GetRequestBodyAsString());
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Bloodweb.json";
            }

            if ((oSession).uriContains("v1/inventories") && !MainWindow.profile.Off)
            {
                if(MainWindow.profile.FullProfile)
                {
                    (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/SkinsWithItems.json";
                }
                else if(MainWindow.profile.Skins_Only)
                {
                    (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/SkinsONLY.json";
                }
                else if(MainWindow.profile.Skins_Perks_Only)
                {
                    (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/SkinsPerks.json";
                }
                else if (MainWindow.profile.DLC_Only)
                {
                    (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/DlcOnly.json";
                }
            }

            if ((oSession).uriContains("api/v1/wallet/currencies") && MainWindow.profile.Currency_Spoof)
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Currency.json";

            if (((oSession).uriContains("api/v1/extensions/playerLevels/getPlayerLevel")  || (oSession).uriContains("api/v1/extensions/playerLevels/earnPlayerXp")) && MainWindow.profile.Level_Spoof)
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Level.json";

            if ((oSession).uriContains("/catalog") && MainWindow.profile.Break_Skins && !MainWindow.profile.Off)
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Catalog.json";

            if ((oSession).uriContains("itemsKillswitch") && MainWindow.profile.Disabled && !MainWindow.profile.Off)
                (oSession).oFlags["x-replywithfile"] = Settings.ProfilePath + "/Disabled.json";

            /*if ((oSession).uriContains("api/v1/queue") && Main.REGIONBOOL)
            {
                oSession.utilSetRequestBody(Main.UpdateLatency(((Session)oSession).GetRequestBodyAsString(), Main.REGION));
            }*/ // Ændre spiller region
        }

        private static void CookieGrabWithoutShutdown(Session oSession)
        {
            if (oSession.uriContains("api/v1/config"))
            {
                if (oSession.oRequest["Cookie"].Length > 0)
                {
                    CookieHandler.SetCookie(oSession.oRequest["Cookie"]);
                }
            }
        }

        private static void CookieGrabWithShutdown(Session oSession)
        {
            if (oSession.uriContains("api/v1/config"))
            {
                if (oSession.oRequest["Cookie"].Length > 0)
                {
                    CookieHandler.SetCookie(oSession.oRequest["Cookie"]);
                    MainWindow.cookie.ReturnFromCookie("Successfully Grabbed Cookie", true);
                    FiddlerApplication.BeforeRequest -= GrabWithShutdown;
                    StopFiddlerCore();
                }
            }
        }
    }
}
