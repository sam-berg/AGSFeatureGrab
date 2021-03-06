﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ESRI.ArcGIS.ArcMap;
using ESRI.ArcGIS.ArcMapUI;
using ESRI.ArcGIS.Desktop;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.SystemUI;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Carto;
using System.Windows.Threading;

namespace AGSFeatureGrab
{

  public partial class UserControlUI3 : UserControl
  {
    IApplication _hook = null;
    private ServerLayer _mapLayer = null;
    int _iMaxRecordCount = 500;
    IList<int> _objectids = null;
    int _iPendingPagedQueries = 0;

    Dictionary<string,string> origFieldNames = new Dictionary<string,string>();

    public UserControlUI3()
    {
      InitializeComponent();
      _hook = ArcMap.Application;

      setReady(false);

    }

    private void clearUI()
    {
      this.txtMapService.Text = "";
      this.lstFields.Items.Clear();
      this.txtQuery.Text = "";
      this.txtFeatureClassName.Text = "";
      this.chkInView.IsChecked = false;
      this.txtFeatureCount.Text = "";
      this.chkPage.IsChecked = false;
      this.txtFeatureCount.IsEnabled = false;
      setReady(false);
    }

    private void setReady(bool bReady)
    {
      this.cmdOK.IsEnabled = bReady;
      this.lstFields.IsEnabled = bReady;
      this.txtQuery.IsEnabled = bReady;
      this.chkInView.IsEnabled = bReady;
      //chkPage.IsEnabled = bReady;
      this.txtPageSize.IsEnabled = bReady;

      chkPage.IsChecked = true;
      this.txtFeatureCount.IsEnabled = (chkPage.IsChecked==true);
      this.txtFeatureClassName.IsEnabled = bReady;
    }

    private void cmdOK_Click(object osender, RoutedEventArgs ee)
    {
      showWorking();

      if (this.chkPage.IsChecked == true)
      {
        doNewPagedQuery();
      }
      else
      {
        doNewQuery();
      }
    }

    private void doNewPagedQuery()
    {
      try
      {

        //this._iMaxRecordCount
        _iPendingPagedQueries = 0;

        string sFeaturesToGet = this.txtFeatureCount.Text;
        int iMaxRecordCount = _iMaxRecordCount;
        int iBegin = 0;
        int iFeatureCount = 0;// Convert.ToInt32(this.txtFeatureCount.Text);

        if (sFeaturesToGet.IndexOf("-") > 0)
        {
          iBegin = Convert.ToInt32(sFeaturesToGet.Split('-')[0]);
          iFeatureCount = Convert.ToInt32(sFeaturesToGet.Split('-')[1]);
          if (iFeatureCount - iBegin < iMaxRecordCount) iMaxRecordCount = iFeatureCount - iBegin;
        }
        else
        {
          iFeatureCount = Convert.ToInt32(this.txtFeatureCount.Text);
        }



        IList<int> objectIDs = _objectids;

        for (int i = iBegin; i < iFeatureCount; i += iMaxRecordCount)
        {
          _iPendingPagedQueries++;
        }

        _pagedFeatureSet = null;
        objectIDs = objectIDs.Skip(iBegin).ToList<int>();

        //this.progressBar.Visibility = Visibility.Visible;
        //this.progressBar.Minimum = iBegin;
        //this.progressBar.Maximum = iFeatureCount;
        _hook.StatusBar.ProgressBar.MinRange = iBegin;
        _hook.StatusBar.ProgressBar.MaxRange = iFeatureCount;
        this._hook.StatusBar.ShowProgressBar("Grabbing Features...", iBegin, iFeatureCount, 1, true);

        ESRI.ArcGIS.Client.Tasks.Query query = new ESRI.ArcGIS.Client.Tasks.Query();
        QueryTask queryTask = new QueryTask(this.txtMapService.Text);
        queryTask.ExecuteCompleted += (object sender, QueryEventArgs e) =>
        { this.PagedQueryTask_ExecuteCompleted(sender, e); };
        queryTask.Failed += PagedQueryTask_Failed;
        
        for (int i = iBegin; i < iFeatureCount; i += iMaxRecordCount)
        {

          //Action a=delegate()
          //{
          //  this.progressBar.Visibility = Visibility.Visible;
          //  this.progressBar.Value = i;
          //};
          //Dispatcher.BeginInvoke(a);
          _hook.StatusBar.ProgressBar.Position = i;
          updateStatus("Grabbing Features...");

          //ESRI.ArcGIS.Client.Tasks.Query query = new ESRI.ArcGIS.Client.Tasks.Query();
          //QueryTask queryTask = new QueryTask(this.txtMapService.Text);

          //sbtest
          if (txtQuery.Text != null && txtQuery.Text.Length > 0)
          {
            query.Where = txtQuery.Text;
          }
          else
          {
            query.Where = "0=0";
          }


          if (lstFields.SelectedItems.Count == lstFields.Items.Count)
          {
            query.OutFields.Add("*");
          }
          else
          {
            for (int iField = 0; iField < lstFields.SelectedItems.Count; iField++)
            {
              query.OutFields.Add(lstFields.SelectedItems[iField].ToString());
            }
          }

          query.ReturnGeometry = true;

          query.ObjectIDs = objectIDs.Take(iMaxRecordCount).ToArray<int>();

          objectIDs = objectIDs.Skip(iMaxRecordCount).ToList<int>();

          try
          {
            FeatureSet fs = queryTask.Execute(query);
            handleFeatureSet(fs);
          }
          catch (Exception ex)
          {
            //if query task did not come back we should _iPendingPagedQueries--;
            //if (ex.Message.IndexOf("underlying connection") > 1) _iPendingPagedQueries--;

            MessageBox.Show(ex.Message, "New Paged Query - handle feature set");

            break;

          }
          


        }


        this._hook.StatusBar.HideProgressBar();
        clearUI();
        hideWorking();
        updateStatus("Grabbing Features Completed.");
      }
      catch(Exception ex)
      {
        MessageBox.Show(ex.Message, "New Paged Query");
        this._hook.StatusBar.HideProgressBar();
      }

      //this.progressBar.Visibility = Visibility.Hidden;
      this._hook.StatusBar.HideProgressBar();
      hideWorking();

    }



    private void showWorking()
    {
      this.Cursor = Cursors.Wait;
    }

    private void hideWorking()
    {
      this.Cursor = Cursors.Arrow;
    }

    private void FeatureCountQueryTask_Failed(object sender, TaskFailedEventArgs args)
    {
      this.txtFeatureCount.Text  ="-1";//500 or 1000, etc
      //this.txtFeatureCount.IsEnabled = false;
      this.chkPage.IsChecked = false;
      this.chkPage.IsEnabled = false;
      setReady(true);
      hideWorking();
    }
    private void FeatureCountQueryTask_ExecuteCompleted(object sender, ESRI.ArcGIS.Client.Tasks.QueryEventArgs args)
    {

      int iCount = args.FeatureSet.ObjectIDs.Count();
      this.txtFeatureCount.Text = iCount.ToString();

      _objectids = new List<int>();

      foreach (object o in args.FeatureSet.ObjectIDs)
      {
        int ii = Convert.ToInt32(o);
        _objectids.Add(ii);
      }
      

      this.chkPage.IsChecked = false;
      this.chkPage.IsEnabled = false;
      if (iCount > 0 && iCount>_iMaxRecordCount)
      {        
        this.chkPage.IsEnabled = true;
      }
      setReady(true);
      hideWorking();

     // this.txtFeatureCount.IsEnabled = true;
    }

    private void OperationalQueryTask_ExecuteCompleted(object sender, ESRI.ArcGIS.Client.Tasks.QueryEventArgs args)
    {
      FeatureSet featureSet = args.FeatureSet;
      IFeatureClass pResultClass = LoadResult(featureSet);

      if (pResultClass != null && pResultClass.FeatureCount(null)>0)
      {
        
        IFeatureLayer pFL = new FeatureLayerClass();
        pFL.Name = this.txtFeatureClassName.Text.Trim().Length>0? this.txtFeatureClassName.Text:_mapLayer.name    ;
        pFL.FeatureClass = pResultClass;
        IMxDocument p = (IMxDocument)_hook.Document;
        p.FocusMap.AddLayer(pFL);
      }
      else
      {
        MessageBox.Show("No features could be retrieved. Perhaps the geometries are not queriable from this layer, or the query is too restrictive.");
      }

      clearUI();
      hideWorking();
    }

    FeatureSet _pagedFeatureSet = null;
    private void PagedQueryTask_ExecuteCompleted(object sender, ESRI.ArcGIS.Client.Tasks.QueryEventArgs args)
    {

      try { 

      
        FeatureSet featureSet = args.FeatureSet;

        handleFeatureSet(featureSet);
        return;


        //_iPendingPagedQueries--;

        //if (_pagedFeatureSet == null)
        //{
        //  _pagedFeatureSet = featureSet;
        //}
        //else
        //{
        //  foreach (Graphic g in featureSet.Features)
        //  {
        //    _pagedFeatureSet.Features.Add(g);
        //  }
        //}

        //System.Diagnostics.Debug.WriteLine("Executed Query #" + _iPendingPagedQueries.ToString());
        //if (_iPendingPagedQueries > 0) return;

        //IFeatureClass pResultClass = LoadResult(_pagedFeatureSet);

        //if (pResultClass != null && pResultClass.FeatureCount(null) > 0)
        //{

        //  IFeatureLayer pFL = new FeatureLayerClass();
        //  pFL.Name = this.txtFeatureClassName.Text.Trim().Length > 0 ? this.txtFeatureClassName.Text : _mapLayer.name;
        //  pFL.FeatureClass = pResultClass;
        //  IMxDocument p = (IMxDocument)_hook.Document;
        //  p.FocusMap.AddLayer(pFL);
        //}
        //else
        //{
        //  MessageBox.Show("No features could be retrieved. Perhaps the geometries are not queriable from this layer, or the query is too restrictive.");
        //}

        //clearUI();
        //hideWorking();
      }
      catch (Exception ex)
      {
        string e = ex.Message;
        MessageBox.Show("Error: " + e);
        clearUI();
        hideWorking();
      }
    }

    private void handleFeatureSet(FeatureSet featureSet)
    {

      try
      {


        _iPendingPagedQueries--;

        if (_pagedFeatureSet == null)
        {
          _pagedFeatureSet = featureSet;
        }
        else
        {
          foreach (Graphic g in featureSet.Features)
          {
            _pagedFeatureSet.Features.Add(g);
          }
        }

        System.Diagnostics.Debug.WriteLine("Executed Query #" + _iPendingPagedQueries.ToString());
        if (_iPendingPagedQueries > 0) return;

        updateStatus("Paged Queries Complete.");
          
        IFeatureClass pResultClass = LoadResult(_pagedFeatureSet);

        if (pResultClass != null && pResultClass.FeatureCount(null) > 0)
        {
          updateStatus("Creating Layer...");
          IFeatureLayer pFL = new FeatureLayerClass();
          pFL.Name = this.txtFeatureClassName.Text.Trim().Length > 0 ? this.txtFeatureClassName.Text : _mapLayer.name;
          pFL.FeatureClass = pResultClass;
          IMxDocument p = (IMxDocument)_hook.Document;
          p.FocusMap.AddLayer(pFL);
        }
        else
        {
          MessageBox.Show("No features could be retrieved. Perhaps the geometries are not queriable from this layer, or the query is too restrictive.");
        }
      }
      catch(Exception ex)
      {

        MessageBox.Show(ex.Message, "Error");
      }

      clearUI();
      hideWorking();

    }
    private void PagedQueryTask_ExecuteCompleted2(object sender, ESRI.ArcGIS.Client.Tasks.QueryEventArgs args)
    {

      try
      {


        FeatureSet featureSet = args.FeatureSet;

        _iPendingPagedQueries--;

        if (_pagedFeatureSet == null)
        {
          _pagedFeatureSet = featureSet;
        }
        else
        {
          foreach (Graphic g in featureSet.Features)
          {
            _pagedFeatureSet.Features.Add(g);
          }
        }

        System.Diagnostics.Debug.WriteLine("Executed Query #" + _iPendingPagedQueries.ToString());
        if (_iPendingPagedQueries > 0) return;

        IFeatureClass pResultClass = LoadResult(_pagedFeatureSet);

        if (pResultClass != null && pResultClass.FeatureCount(null) > 0)
        {

          IFeatureLayer pFL = new FeatureLayerClass();
          pFL.Name = this.txtFeatureClassName.Text.Trim().Length > 0 ? this.txtFeatureClassName.Text : _mapLayer.name;
          pFL.FeatureClass = pResultClass;
          IMxDocument p = (IMxDocument)_hook.Document;
          p.FocusMap.AddLayer(pFL);
        }
        else
        {
          MessageBox.Show("No features could be retrieved. Perhaps the geometries are not queriable from this layer, or the query is too restrictive.");
        }

        clearUI();
        hideWorking();
      }
      catch (Exception ex)
      {
        string e = ex.Message;
        MessageBox.Show("Error: " + e,"Paged Query Task Error");
        clearUI();
        hideWorking();
      }
    }

    
    private void updateStatus(string sStatus)
    {
      System.Diagnostics.Debug.WriteLine("updateStatus: " + sStatus);
      this._hook.StatusBar.set_Message(0,sStatus);

    }


    private IFeatureClass LoadResult(FeatureSet featureSet)
    {
      IFeatureClass pFC = null;
      string sError = null;

      try
      {
        IWorkspace pWorkspace = CreateInMemoryWorkspace();
        string sFeatureLayerName = this.txtFeatureClassName.Text.Trim().Length > 0 ? getValidFieldName( this.txtFeatureClassName.Text) : getValidFieldName( _mapLayer.name);
        IFields fieldsCollection = CreateFields();

        pFC = CreateStandaloneFeatureClass(pWorkspace, sFeatureLayerName, fieldsCollection, "SHAPE");

        if (pFC == null)
        {
          MessageBox.Show("Error creating target feature class.");
          return null;
        }

        int iCount = 0;
        foreach (ESRI.ArcGIS.Client.Graphic pElement in featureSet.Features)
        {

          IGeometry pTargetAOGeom = getGeometryFromElement(pElement);

          if (pTargetAOGeom != null)
          {
            IFeature pFeat = pFC.CreateFeature();
            copyAttributes(pElement, pFeat);

            pFeat.Shape = pTargetAOGeom;
            pFeat.Store();
            iCount++;
          }

        }
      }
      catch (Exception ex)
      {
        sError = ex.Message;

      }
      if (sError != null) MessageBox.Show(sError,"Load Result");
      return pFC;

    }

    private void copyAttributes(Graphic pElement, IFeature pFeat)
    {

      try
      {
        for (int i = 0; i < pFeat.Fields.FieldCount; i++)
        {
          if (pFeat.Fields.Field[i].Type != esriFieldType.esriFieldTypeGeometry && pFeat.Fields.Field[i].Type != esriFieldType.esriFieldTypeOID && pFeat.Fields.Field[i].Type != esriFieldType.esriFieldTypeGlobalID)
          {

            string sFieldName = pFeat.Fields.Field[i].Name;

            string sOrigName = sFieldName;
            if (origFieldNames.ContainsKey(sFieldName))
              sOrigName = origFieldNames[sFieldName];

            object oValue = pElement.Attributes[sOrigName];

            // pFeat.Value[pFeat.Fields.FindField(sFieldName)] = oValue;
            pFeat.Value[pFeat.Fields.FindField(sFieldName)] = oValue;

          }
        }
      }
      catch(Exception ex)
      {

        MessageBox.Show(ex.Message, "copyAttributes");
      }

    }

    private IGeometry ClientPointToArcObjectsPoint(ESRI.ArcGIS.Client.Geometry.MapPoint pMapPoint)
    {

        ESRI.ArcGIS.Client.Geometry.MapPoint pInPoint = pMapPoint;
        IPoint pPoint = new PointClass();
        pPoint.X = pInPoint.X;
        pPoint.Y = pInPoint.Y;
        return pPoint;

    }

    private IGeometry ClientPolygonToArcObjectsPolygon(ESRI.ArcGIS.Client.Geometry.Polygon pMapPolygon)
    {
      ESRI.ArcGIS.Client.Geometry.Polygon pInPoly = pMapPolygon;
      IPolygon pPolygon = new PolygonClass();
      IGeometryCollection geometryCollection = pPolygon as IGeometryCollection;


      for (int iRings = 0; iRings < pInPoly.Rings.Count; iRings++)
      {
        ESRI.ArcGIS.Client.Geometry.PointCollection pC = pInPoly.Rings[iRings];

        IPointCollection pPolyRing = new RingClass();
        for (int ii = 0; ii < pC.Count; ii++)
        {
          IPoint pp = new PointClass() { X = pC[ii].X, Y = pC[ii].Y };
          pPolyRing.AddPoint(pp);
        }

        IRing pRing = pPolyRing as IRing;
        geometryCollection.AddGeometry(pRing as IGeometry);

      }

      return pPolygon;

    }

    private IGeometry ClientPolylineToArcObjectsPolyline(ESRI.ArcGIS.Client.Geometry.Polyline pMapPolyline)
    {
      ESRI.ArcGIS.Client.Geometry.Polyline pInLine = pMapPolyline;
      IPolyline pPolyline= new PolylineClass();
      IGeometryCollection geometryCollection = pPolyline as IGeometryCollection;
      IPointCollection pPC = (IPointCollection)pPolyline;

      for (int iPaths= 0; iPaths < pInLine.Paths.Count; iPaths++)
      {
        ESRI.ArcGIS.Client.Geometry.PointCollection pC = pInLine.Paths[iPaths];
        //ESRI.ArcGIS.Geometry.ILine line = new ESRI.ArcGIS.Geometry.LineClass();
        //ILine pLine = new LineClass();
        
        for(int iP=0;iP<pC.Count;iP++)
        {
          IPoint pp = new PointClass() { X = pC[iP].X, Y = pC[iP].Y };
          pPC.AddPoint(pp);
        }

        
        //IPoint pp2 = new PointClass() { X = pC[1].X, Y = pC[1].Y };
        //pLine.FromPoint = pp;
        //pLine.ToPoint = pp2;
        //geometryCollection.AddGeometry(pLine);

        
      }
      

      return pPolyline;
    }

    private IGeometry getGeometryFromElement(ESRI.ArcGIS.Client.Graphic pElement)
    {

      if (_mapLayer.geometryType == "esriGeometryPoint")
      {
        return ClientPointToArcObjectsPoint(pElement.Geometry as ESRI.ArcGIS.Client.Geometry.MapPoint );
      }
      else if (_mapLayer.geometryType == "esriGeometryPolygon")
      {
        return ClientPolygonToArcObjectsPolygon(pElement.Geometry as ESRI.ArcGIS.Client.Geometry.Polygon);
      }
      else if (_mapLayer.geometryType == "esriGeometryPolyline")
      {
        return ClientPolylineToArcObjectsPolyline(pElement.Geometry as ESRI.ArcGIS.Client.Geometry.Polyline);
      }      

      return null;
    }

    private IFields CreateFields()
    {

      // Create a fields collection.
      IFields fields = new FieldsClass();
      origFieldNames = new Dictionary<string, string>();

      // Cast to IFieldsEdit to modify the properties of the fields collection.
      IFieldsEdit fieldsEdit = (IFieldsEdit)fields;

      // Create the ObjectID field.
      IField oidField = new FieldClass();

      // Cast to IFieldEdit to modify the properties of the new field.
      IFieldEdit oidFieldEdit = (IFieldEdit)oidField;
      oidFieldEdit.Name_2 = "ObjectID";
      oidFieldEdit.AliasName_2 = "FID";
      oidFieldEdit.Type_2 = esriFieldType.esriFieldTypeOID;
      fieldsEdit.AddField(oidField);

      // Create the SHAPE field.
      IField shapeField = new FieldClass();

      // Cast to IFieldEdit to modify the properties of the new field.
      IFieldEdit shapeFieldEdit = (IFieldEdit)shapeField;
      shapeFieldEdit.Name_2 = "SHAPE";
      shapeFieldEdit.AliasName_2 = "SHAPE";
      shapeFieldEdit.Type_2 = esriFieldType.esriFieldTypeGeometry;
      

      IGeometryDef pGeomDef;
      IGeometryDefEdit pGeomDefEdit;
      pGeomDef = new GeometryDefClass();
      pGeomDefEdit = pGeomDef as IGeometryDefEdit;
      pGeomDefEdit.GeometryType_2 = getGeometryType(_mapLayer.geometryType);
      shapeFieldEdit.GeometryDef_2 = pGeomDef;

      //todo, needs work!
      ISpatialReference spatialRef = new UnknownCoordinateSystemClass();
      if (_mapLayer.extent.SpatialReference == null || _mapLayer.extent.SpatialReference.WKID==0)
      {
        spatialRef = new UnknownCoordinateSystemClass();
        if (_mapLayer.extent == null)
        {
          spatialRef.SetDomain(-1993654.562, +1993654.562, -9968262.534, +9968262.534);
        }
        else
        {
          spatialRef.SetDomain(_mapLayer.extent.XMin - 1000000, _mapLayer.extent.YMin - 1000000, _mapLayer.extent.XMax + 1000000, _mapLayer.extent.YMax + 1000000);      
        }
        MessageBox.Show("Note: the spatial reference of the map service layer is not known and the output will have an Unknown Coordinate System.", "Unknown Spatial Reference", MessageBoxButton.OK, MessageBoxImage.Exclamation);

      }
      else if (_mapLayer.extent.SpatialReference.WKID > 4000 && _mapLayer.extent.SpatialReference.WKID <5000)
      {
        ISpatialReferenceFactory3 p=new SpatialReferenceEnvironmentClass();
        spatialRef = p.CreateGeographicCoordinateSystem(_mapLayer.extent.SpatialReference.WKID);
        spatialRef.SetDomain(-200, 200, -110, 110);
      }
      else
      {
        ISpatialReferenceFactory3 p=new SpatialReferenceEnvironmentClass();
        
        spatialRef=p.CreateProjectedCoordinateSystem(_mapLayer.extent.SpatialReference.WKID);
        //ISpatialReference3 pSBSR =  _mapLayer.extent.SpatialReference;

      //sbtest spatialRef.SetDomain(_mapLayer.extent.XMin - 10000000, _mapLayer.extent.YMin - 10000000, _mapLayer.extent.XMax + 10000000, _mapLayer.extent.YMax + 10000000);
        //spatialRef.SetDomain(_mapLayer.extent.XMin , _mapLayer.extent.YMin, _mapLayer.extent.XMax, _mapLayer.extent.YMax );
     
      }

      pGeomDefEdit.SpatialReference_2 = spatialRef; 
      //_mapLayer.extent.SpatialReference.WKID

      fieldsEdit.AddField(shapeField);

      for (int i = 0; i < lstFields.SelectedItems.Count; i++)
      {
        string sFieldName = lstFields.SelectedItems[i].ToString();
        ServerLayerField pField = getField(sFieldName);

        if (pField.type != "esriFieldTypeOID" && pField.type!="esriFieldTypeGeometry")
        {
          // Create the ObjectID field.
          IField ppField = new FieldClass();

          // Cast to IFieldEdit to modify the properties of the new field.
          IFieldEdit ppFieldEdit = (IFieldEdit)ppField;

          string sOFieldName=pField.name;
          string sNewName = getValidFieldName(pField.name);
          ppFieldEdit.Name_2 = sNewName;// getValidFieldName(pField.name);
          origFieldNames.Add(sNewName,sOFieldName );
          

          ppFieldEdit.AliasName_2 = pField.alias;
          ppFieldEdit.Type_2 = getFieldType(pField.type);
          fieldsEdit.AddField(ppField);
        }

      }

      return fields;
    }

    private string getValidFieldName(string sInputName)
    {
      

      string s = "";
      s = sInputName.Replace(".", "_");//sbtest todo
      s = s.Replace(" ", "_");

      return s;

    }

    private esriGeometryType getGeometryType(string sType)
    {
      if (sType == "esriGeometryPolyline") return esriGeometryType.esriGeometryPolyline;
      if (sType == "esriGeometryPoint") return esriGeometryType.esriGeometryPoint;
      if (sType == "esriGeometryPolygon") return esriGeometryType.esriGeometryPolygon;
      

      return esriGeometryType.esriGeometryPoint;

    }
    
    private esriFieldType getFieldType(string sType)
    {

      if (sType == "esriFieldTypeBlob") return esriFieldType.esriFieldTypeBlob;
      if (sType == "esriFieldTypeDate") return esriFieldType.esriFieldTypeDate;
      if (sType == "esriFieldTypeDouble") return esriFieldType.esriFieldTypeDouble;
      if (sType == "esriFieldTypeGlobalID") return esriFieldType.esriFieldTypeGlobalID;
      if (sType == "esriFieldTypeGUID") return esriFieldType.esriFieldTypeGUID;
      if (sType == "esriFieldTypeInteger") return esriFieldType.esriFieldTypeInteger;
      if (sType == "esriFieldTypeRaster") return esriFieldType.esriFieldTypeRaster;
      if (sType == "esriFieldTypeSingle") return esriFieldType.esriFieldTypeSingle;
      if (sType == "esriFieldTypeSmallInteger") return esriFieldType.esriFieldTypeSmallInteger;
      if (sType == "esriFieldTypeString") return esriFieldType.esriFieldTypeString;
      if (sType == "esriFieldTypeXML") return esriFieldType.esriFieldTypeXML;

      return esriFieldType.esriFieldTypeString;
    }

    private ServerLayerField getField(string sName)
    {
      for (int i = 0; i < _mapLayer.fields.Length; i++)
      {
        if (_mapLayer.fields[i].name.ToUpper() == sName.ToUpper())
        {
          return _mapLayer.fields[i];
        }
      }
      return null;
    }

    public IFeatureClass CreateStandaloneFeatureClass(IWorkspace workspace, String
    featureClassName, IFields fieldsCollection, String shapeFieldName)
    {
      IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspace;
      IFeatureClassDescription fcDesc = new FeatureClassDescriptionClass();
      IObjectClassDescription ocDesc = (IObjectClassDescription)fcDesc;

      // Use IFieldChecker to create a validated fields collection.
      IFieldChecker fieldChecker = new FieldCheckerClass();
      IEnumFieldError enumFieldError = null;
      IFields validatedFields = null;
      fieldChecker.ValidateWorkspace = workspace;
      fieldChecker.Validate(fieldsCollection, out enumFieldError, out validatedFields);

      // The enumFieldError enumerator can be inspected at this point to determine 
      // which fields were modified during validation.
      IFeatureClass featureClass = featureWorkspace.CreateFeatureClass
          (featureClassName, validatedFields, ocDesc.InstanceCLSID,
          ocDesc.ClassExtensionCLSID, esriFeatureType.esriFTSimple, shapeFieldName, "")
          ;
      return featureClass;
    }
    
    private IWorkspace CreateInMemoryWorkspace()
    {
      // Create an InMemory workspace factory.
      IWorkspaceFactory workspaceFactory = new InMemoryWorkspaceFactoryClass();

      // Create an InMemory geodatabase.
      IWorkspaceName workspaceName = workspaceFactory.Create("", "AGSFeatureGrab",
        null, 0);

      // Cast for IName.
      IName name = (IName)workspaceName;

      //Open a reference to the InMemory workspace through the name object.
      IWorkspace workspace = (IWorkspace)name.Open();
      return workspace;
    }

    private void PagedQueryTask_Failed(object sender, TaskFailedEventArgs args)
    {
      _iPendingPagedQueries--;
      MessageBox.Show("Debug:  " + args.Error.ToString());

    }
    private void OperationalQueryTask_Failed(object sender, TaskFailedEventArgs args)
    {
      string sError= "";

      if (args.Error.Message == null || args.Error.Message == "")
      {
        sError = "Perhaps the GIS Server is not set up for cross domain data queries.";
      }
      else
      {
        sError = args.Error.Message;
      }
      MessageBox.Show("Query failed: " + args.Error.Message);
      hideWorking();
    }

    private void doNewQuery()
    {
      ESRI.ArcGIS.Client.Tasks.Query query = new ESRI.ArcGIS.Client.Tasks.Query();
      QueryTask queryTask = new QueryTask(this.txtMapService.Text);
      
      queryTask.ExecuteCompleted += (object sender, QueryEventArgs e) =>
      { this.OperationalQueryTask_ExecuteCompleted(sender, e); };
      queryTask.Failed += OperationalQueryTask_Failed;

      for (int i = 0; i < lstFields.SelectedItems.Count; i++)
      {
        query.OutFields.Add(lstFields.SelectedItems[i].ToString()); 
      }
      
      if (chkInView.IsChecked==true )
      {
        IMxDocument p=(IMxDocument)_hook.Document;
        ESRI.ArcGIS.Carto.IActiveView pAV = (ESRI.ArcGIS.Carto.IActiveView)p.FocusMap;
        query.Geometry = new ESRI.ArcGIS.Client.Geometry.Envelope( pAV.Extent.XMin,pAV.Extent.YMin,pAV.Extent.XMax,pAV.Extent.YMax);
        if(p.FocusMap.SpatialReference!=null)query.Geometry.SpatialReference = new ESRI.ArcGIS.Client.Geometry.SpatialReference(p.FocusMap.SpatialReference.FactoryCode);
      }
      else
      {
        query.Geometry = null;
      }

      if (txtQuery.Text!=null&&txtQuery.Text.Length>0)
      {        
        query.Where = txtQuery.Text;
      }
      else
      {
        query.Where = "0=0";
      }
      
      query.ReturnGeometry = true;

      queryTask.ExecuteAsync(query);

    }

    void proxy_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e, string sURL)
    {

      System.IO.Stream strm = e.Result as System.IO.Stream;
      System.Runtime.Serialization.Json.DataContractJsonSerializer ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ServerLayer));
      ServerLayer mapL = (ServerLayer)ser.ReadObject(strm);

      if(mapL==null || (mapL.name==null && mapL.fields==null && mapL.geometryType==null))
      {
        //problem connecting
        MessageBox.Show ("There was a problem connecting to the Map Service Layer."  + Environment.NewLine + "Please ensure your URL is in the format: http://server/<arcgis>/rest/services/<mapserver>/<layerid>");
        hideWorking();
        return;
      }
      _mapLayer = mapL;

      this.lstFields.Items.Clear();
      for(int i=0;i<mapL.fields.Length;i++)
      {
        ServerLayerField field = mapL.fields[i];

        this.lstFields.Items.Add(field.name);
        
      }

      this.txtFeatureClassName.Text = _mapLayer.name;

      this.lstFields.SelectAll();

      if (0 == 0)
      {
        getFeatureCount(sURL);

        getMaximumFeatureCount(sURL);
      }
      else
      {
        setReady(true);
        hideWorking();
      }
    }

    private int getMaximumFeatureCount(string sURL)
    {
      if(sURL.ToUpper().IndexOf("FEATURESERVER")>0) return 0;

      MapServer.ESRI_Census_USA_MapServer mapservice = new MapServer.ESRI_Census_USA_MapServer();

      string sMapURL = sURL;
      sMapURL =sMapURL.Substring(0,sMapURL.LastIndexOf("/")).Replace("/rest/","/").Replace("/REST/","/").Replace("/Rest/","/");
      mapservice.Url = sMapURL;

      string mapname = mapservice.GetDefaultMapName();
      MapServer.PropertySet serviceproperties = mapservice.GetServiceConfigurationInfo();

      MapServer.PropertySetProperty[] propertyarray = serviceproperties.PropertyArray;

      foreach (MapServer.PropertySetProperty serviceprop in propertyarray)
      {

        string key = serviceprop.Key.ToString();

        string value = serviceprop.Value.ToString();

        if (key.ToUpper() == "MaximumRecordCount".ToUpper())
        {
          int iCount = Convert.ToInt32(value);

          _iMaxRecordCount = iCount;
          this.txtPageSize.Text = "(" + _iMaxRecordCount + ")";
          return iCount;
        }

      }

      return 0;

    }

    private void getFeatureCount(string sURL)
    {
      ESRI.ArcGIS.Client.Tasks.Query query = new ESRI.ArcGIS.Client.Tasks.Query();
      QueryTask queryTask = new QueryTask(sURL);

      queryTask.ExecuteCompleted += (object sender, QueryEventArgs e) =>
      { this.FeatureCountQueryTask_ExecuteCompleted(sender, e); };
      queryTask.Failed += FeatureCountQueryTask_Failed;

      query.ReturnIdsOnly = true;
      query.Where = "0=0";

      queryTask.ExecuteAsync(query);
    }

    private void cmdConnect_Click(object osender, RoutedEventArgs ee)
    {
      string sURL = this.txtMapService.Text;

      if ((sURL.ToUpper().IndexOf("HTTP") < 0 && sURL.ToUpper().IndexOf("WWW") < 0) || (sURL.ToUpper().IndexOf("MAPSERVER") < 0 &&  sURL.ToUpper().IndexOf("FEATURESERVER") < 0))
      {
        MessageBox.Show("This does not appear to be a valid Map or Feature Server Layer URL.");
        return;
      }

      setReady(false);

      showWorking();

      
      WebClient proxy = new WebClient();
      proxy.OpenReadCompleted += (object sender, OpenReadCompletedEventArgs e) =>
      { this.proxy_OpenReadCompleted(sender, e, sURL); };
      
      proxy.OpenReadAsync(new Uri(sURL + "?f=json"));
    }

    private void txtMapService_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        cmdConnect_Click(null, null);
      }
    }


    private void chkPage_Unchecked(object sender, RoutedEventArgs e)
    {
      this.txtFeatureCount.IsEnabled = (chkPage.IsChecked == true);
    }

    private void chkPage_Checked_1(object sender, RoutedEventArgs e)
    {
      this.txtFeatureCount.IsEnabled = (chkPage.IsChecked == true);
    }

  }

  public class ServerLayer
  {
    public string id { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public string geometryType { get; set; }
    public string description { get; set; }
    public string definitionExpression { get; set; }
    public string copyrightText { get; set; }
    public double minScale { get; set; }
    public double maxScale { get; set; }
    public ESRI.ArcGIS.Client.Geometry.Envelope extent { get; set; }
    public string displayField { get; set; }
    public ServerLayerField[] fields { get; set; }
    public ServerLayer parentLayer { get; set; }
    public ServerLayer[] subLayers { get; set; }

  }


  public class ServerLayerField
  {
    public string name { get; set; }
    public string type { get; set; }
    public string alias { get; set; }
  }


}
