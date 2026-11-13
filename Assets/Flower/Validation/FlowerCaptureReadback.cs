using System;
using UnityEngine;

namespace MindState {
// Camera HDR targets contain linear, already tone-mapped values. Encode display
// pixels through an sRGB render target before CPU readback; never adjust exposure.
sealed class FlowerCaptureReadback:IDisposable {
 readonly RenderTexture display;
 public FlowerCaptureReadback(int width,int height){display=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);display.Create();}
 public void Read(RenderTexture source,Texture2D image){
  var active=RenderTexture.active;bool srgb=GL.sRGBWrite;
  try{
   GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
   Graphics.Blit(source,display);
   RenderTexture.active=display;image.ReadPixels(new Rect(0,0,display.width,display.height),0,0);
  }finally{GL.sRGBWrite=srgb;RenderTexture.active=active;}
 }
 public void Dispose(){display.Release();UnityEngine.Object.Destroy(display);}
#if UNITY_EDITOR || FLOWER_VALIDATION
 internal static string VerifyTransfer(){
  var linear=new RenderTexture(4,4,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);linear.Create();
  var image=new Texture2D(4,4,TextureFormat.RGB24,false);var roundTrip=new Texture2D(4,4,TextureFormat.RGB24,false);
  var active=RenderTexture.active;bool srgb=GL.sRGBWrite;float maxError=0;
  try{using(var first=new FlowerCaptureReadback(4,4))using(var second=new FlowerCaptureReadback(4,4)){
   foreach(float value in new[]{0f,.003f,.018f,.18f,.5f,1f}){
    RenderTexture.active=linear;GL.sRGBWrite=false;GL.Clear(false,true,new Color(value,value,value,1));first.Read(linear,image);
    float encoded=image.GetPixels32()[0].r/255f,expected=QualitySettings.activeColorSpace==ColorSpace.Linear?Mathf.LinearToGammaSpace(value):value;
    maxError=Mathf.Max(maxError,Mathf.Abs(encoded-expected));
    second.Read(first.display,roundTrip);
    if(Mathf.Abs(roundTrip.GetPixels32()[0].r/255f-encoded)>1.1f/255)throw new Exception("Capture applied sRGB transfer twice");
   }
  }}finally{RenderTexture.active=active;GL.sRGBWrite=srgb;linear.Release();UnityEngine.Object.Destroy(linear);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(roundTrip);}
  if(maxError>2f/255)throw new Exception("Capture linear/sRGB calibration failed: "+maxError);
  return "PASS linear HDR -> sRGB readback; dark/mid/white steps, sRGB round trip; max encoded error="+maxError;
 }
#endif
}
}
