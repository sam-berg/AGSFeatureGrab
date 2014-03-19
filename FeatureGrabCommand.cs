using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace AGSFeatureGrab
{
  public class FeatureGrabCommand : ESRI.ArcGIS.Desktop.AddIns.Button
  {
    public FeatureGrabCommand()
    {
    }

    protected override void OnClick()
    {
      ESRI.ArcGIS.Framework.IApplication pApp = (ESRI.ArcGIS.Framework.IApplication)this.Hook;
      ESRI.ArcGIS.Framework.IDockableWindowManager pDWM = (ESRI.ArcGIS.Framework.IDockableWindowManager)pApp;
      ESRI.ArcGIS.esriSystem.UIDClass pUID=new ESRI.ArcGIS.esriSystem.UIDClass();
      pUID.Value="Microsoft_AGSFeatureGrab_FeatureGrabDockableWindow";
      ESRI.ArcGIS.Framework.IDockableWindow pDW= pDWM.GetDockableWindow(pUID);

      pDW.Show(!pDW.IsVisible());


    }

    protected override void OnUpdate()
    {
    }
  }
}
