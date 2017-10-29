using System.Collections;
using System.Collections.Generic;
using System.IO;
using UniLinq;
using UnityEngine;

namespace BDArmory.Shaders
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class BDAShaderLoader : MonoBehaviour
    {
        private static bool _loaded;

        private static string _bundlePath;


        public static Shader GrayscaleEffectShader;
        public static Shader UnlitBlackShader;
        public static Shader BulletShader;
        public static Shader RCSShader;

        public string BundlePath
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "bdarmoryshaders_macosx.bundle";
                    case RuntimePlatform.WindowsPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "bdarmoryshaders_windows.bundle";
                    case RuntimePlatform.LinuxPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "bdarmoryshaders_macosx.bundle";
                    default:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "bdarmoryshaders_windows.bundle";
                }
            }
        }

        public string RCSBundlePath
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                                "bda_rcs_shader_macosx.ksp";
                    case RuntimePlatform.WindowsPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                                "bda_rcs_shader.ksp";
                    case RuntimePlatform.LinuxPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                                "bda_rcs_shader_linux.ksp";
                    default:
                        return _bundlePath + Path.DirectorySeparatorChar +
                                "bda_rcs_shader.ksp";
                }

            }
        }

        private void Awake()
        {
            _bundlePath = KSPUtil.ApplicationRootPath + "GameData" +
                                                    Path.DirectorySeparatorChar +
                                                    "BDArmory" + Path.DirectorySeparatorChar + "AssetBundles";
        }
        private void Start()
        {
            if (!_loaded)
            {
                Debug.Log("[BDArmory] start bundle load process");
                //StartCoroutine(LoadBundleAssets());
                LoadBundleAssets();
                _loaded = true;
            }
        }

        private void LoadBundleAssets()
        {
            Debug.Log("[BDArmory] Loading bundle data");

            AssetBundle shaderBundle = AssetBundle.LoadFromFile(BundlePath);
            AssetBundle rcsBundle = AssetBundle.LoadFromFile(RCSBundlePath);

            if (shaderBundle != null)
            {
                Shader[] shaders = shaderBundle.LoadAllAssets<Shader>();
                IEnumerator<Shader> shader = shaders.AsEnumerable().GetEnumerator();
                while (shader.MoveNext())
                {
                    if (shader.Current == null) continue;
                    Debug.Log($"[BDArmory] Shader \"{shader.Current.name}\" loaded. Shader supported? {shader.Current.isSupported}");

                    switch (shader.Current.name)
                    {
                        case "BDArmory/Particles/Bullet":
                            BulletShader = shader.Current;
                            break;
                        case "Custom/Unlit Black":
                            UnlitBlackShader = shader.Current;
                            break;
                        case "Hidden/Grayscale Effect":
                            GrayscaleEffectShader = shader.Current;
                            break;
                        default:
                            Debug.Log($"[BDArmory] Not expected shader : {shader.Current.name}");
                            break;
                    }
                }

                shader.Dispose();
                //yield return null;
                Debug.Log("[BDArmory] unloading bundles");
                shaderBundle.Unload(false); // unload the raw asset bundle
            }
            else
            {
                Debug.Log("[BDArmory] Error: Found no asset bundle to load");
            }


            if (rcsBundle != null)
            {
                Shader[] rcsshaders = rcsBundle.LoadAllAssets<Shader>();
                List<Shader>.Enumerator rcsshader = rcsshaders.ToList().GetEnumerator();
                while (rcsshader.MoveNext())
                {
                    if (rcsshader.Current == null) continue;
                    Debug.Log($"[BDArmory] Shader \"{rcsshader.Current.name}\" loaded. Shader supported? {rcsshader.Current.isSupported}");

                    switch (rcsshader.Current.name)
                    {
                        case "Custom/rcsShader":
                            RCSShader = rcsshader.Current;
                            break;
                        default:
                            Debug.Log($"[BDArmory] Not expected shader : {rcsshader.Current.name}");
                            break;
                    }
                }

                rcsshader.Dispose();
                //yield return null;
                Debug.Log("[BDArmory] unloading rcsbundles");
                rcsBundle.Unload(false); // unload the raw asset bundle

            }
            else
            {
                Debug.Log("[BDArmory] Error: Found no RCS asset bundle to load");
            }

            //yield return null;
        }
    }
}