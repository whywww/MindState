using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace MindState.Editor
{
    public static class SilkEditorProbe
    {
        public static void Run()
        {
            Directory.CreateDirectory("Evidence/SilkMicroDetail/EditorGPU");
            Profiler.enabled=true;
            Profiler.SetAreaEnabled(ProfilerArea.GPU,true);
            var driver=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
            string info="Editor GPU area enabled="+Profiler.GetAreaEnabled(ProfilerArea.GPU)+"\n";
            if(driver!=null)
            {
                const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
                var gpuProperty=driver.GetProperty("profileGPU",flags);
                if(gpuProperty!=null&&gpuProperty.CanWrite) gpuProperty.SetValue(null,true);
                foreach(string name in new[]{"profileGPU","isGPUProfilerSupportedByOS","isGPUProfilerSupported","isGPUProfilerBuggyOnDriver"})
                {
                    var property=driver.GetProperty(name,flags);
                    if(property!=null) info+=name+"="+property.GetValue(null)+"\n";
                }
                foreach(var method in driver.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                {
                    if(method.Name.IndexOf("GPU",System.StringComparison.OrdinalIgnoreCase)<0) continue;
                    info+=method+"\n";
                    if(method.Name=="IsGPUProfilerSupportedByOS"&&method.GetParameters().Length==0)
                        info+="OS capability="+method.Invoke(null,null)+"\n";
                }
            }
            File.WriteAllText("Evidence/SilkMicroDetail/EditorGPU/Capabilities.txt",info);
            EditorSceneManager.OpenScene("Assets/HeroPetal/Scenes/HeroPetalGallery.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
