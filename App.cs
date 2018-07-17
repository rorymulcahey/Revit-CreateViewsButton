#region Namespaces
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Windows.Forms;

//Requires PresentationCore Addin to become active
using System.Windows.Media.Imaging;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion


namespace CreateViewsButton
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Create a custom ribbon tab
            String tabName = "VantageTCG";
            application.CreateRibbonTab(tabName);

            // Create a ribbon panel; First arg adds panel to custom tab
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "Sheet Tools");

            // Button details and its link to the command
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            PushButtonData buttonData = new PushButtonData("cmdName",
                "Create Multiple Sheets", thisAssemblyPath, typeof(Command).FullName);

            // Does the same as the above
            // PushButtonData buttonData = new PushButtonData("cmdDeleteViews",
            //     "Delete Views", thisAssemblyPath, "RevitWarnings.DeleteViews");

            // Creates a Button
            PushButton pushButton = ribbonPanel.AddItem(buttonData) as PushButton;

            pushButton.ToolTip = "Select the views that need to have a sheet. When all desired views have been chosen, " +
                "each view will appear on its own sheet, centered in view.";

            // Adds picture for button
            string filename = @"\CreateViews.png";
            string currentDir = AssemblyDirectory;
            //Uri uriImage = new Uri(@"%APPDATA%\Autodesk\Revit\Addins\2015\Views.png");
            //Uri uriImage = new Uri(@"C:\Users\rorymulcahey\AppData\Roaming\Autodesk\Revit\Addins\2015\Views.png");
            Uri uriImage = new Uri(currentDir + filename);
            BitmapImage largeImage = new BitmapImage(uriImage);
            pushButton.LargeImage = largeImage;

            return Result.Succeeded; // must return
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded; // must return
        }

        // Finds the current directory
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }


    // Command that will be executed upon button click event
    /// <summary>
    /// Implements the Revit add-in interface IExternalCommand
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        #region IExternalCommand Members Implementation
        /// <summary>
        /// Implement this method as an external command for Revit.
        /// </summary>
        /// <param name="commandData">An object that is passed to the external application 
        /// which contains data related to the command, 
        /// such as the application object and active view.</param>
        /// <param name="message">A message that can be set by the external application 
        /// which will be displayed if a failure or cancellation is returned by 
        /// the external command.</param>
        /// <param name="elements">A set of elements to which the external application 
        /// can add elements that are to be highlighted in case of failure or cancellation.</param>
        /// <returns>Return the status of the external command. 
        /// A result of Succeeded means that the API external method functioned as expected. 
        /// Cancelled can be used to signify that the user cancelled the external operation 
        /// at some point. Failure should be returned if the application is unable to proceed with 
        /// the operation.</returns>
        public Autodesk.Revit.UI.Result Execute(Autodesk.Revit.UI.ExternalCommandData commandData,
            ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            Transaction newTran = null;
            try
            {
                if (null == commandData)
                {
                    throw new ArgumentNullException("commandData");
                }

                Document doc = commandData.Application.ActiveUIDocument.Document;
                ViewsMgr view = new ViewsMgr(doc);

                newTran = new Transaction(doc);
                newTran.Start("AllViews_Sample");

                AllViewsForm dlg = new AllViewsForm(view);

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    view.GenerateSheets(doc);
                }
                newTran.Commit();

                return Autodesk.Revit.UI.Result.Succeeded;
            }
            catch (Exception e)
            {
                // message = e.Message;
                if (e.Message == "viewId cannot be added to the ViewSheet." + Environment.NewLine + "Parameter name: viewId")
                {
                    TaskDialog.Show("Error", "Cannot reuse the same view");
                }
                else
                {
                    message = e.Message;
                }
                if ((newTran != null) && newTran.HasStarted() && !newTran.HasEnded())
                    newTran.RollBack();
                return Autodesk.Revit.UI.Result.Failed;
            }
        }

        #endregion IExternalCommand Members Implementation
    }

    /// <summary>
    /// Generating a new sheet that has all the selected views placed in.
    /// </summary>
    public class ViewsMgr
    {
        private TreeNode m_allViewsNames = new TreeNode("Views (all)");
        private ViewSet m_allViews = new ViewSet();
        private ViewSet m_selectedViews = new ViewSet();
        private FamilySymbol m_titleBlock;
        private IList<Element> m_allTitleBlocks = new List<Element>();
        private ArrayList m_allTitleBlocksNames = new ArrayList();
        private string m_sheetName;
        private double m_rows;

        private double TITLEBAR = 0.11;
        private double GOLDENSECTION = 0.618;

        /// <summary>
        /// Tree node store all views' names.
        /// </summary>
        public TreeNode AllViewsNames
        {
            get
            {
                return m_allViewsNames;
            }
        }

        /// <summary>
        /// List of all title blocks' names.
        /// </summary>
        public ArrayList AllTitleBlocksNames
        {
            get
            {
                return m_allTitleBlocksNames;
            }
        }

        /// <summary>
        /// The selected sheet's name.
        /// </summary>
        public string SheetName
        {
            get
            {
                return m_sheetName;
            }
            set
            {
                m_sheetName = value;
            }
        }

        /// <summary>
        /// Constructor of views object.
        /// </summary>
        /// <param name="doc">the active document</param>
        public ViewsMgr(Document doc)
        {
            GetAllViews(doc);
            GetTitleBlocks(doc);
        }

        /// <summary>
        /// Finds all the views in the active document.
        /// </summary>
        /// <param name="doc">the active document</param>
        private void GetAllViews(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilteredElementIterator itor = collector.OfClass(typeof(Autodesk.Revit.DB.View)).GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                Autodesk.Revit.DB.View view = itor.Current as Autodesk.Revit.DB.View;
                // skip view templates because they're invisible in project browser
                if (null == view || view.IsTemplate)
                {
                    continue;
                }
                else
                {
                    ElementType objType = doc.GetElement(view.GetTypeId()) as ElementType;
                    if (null == objType || objType.Name.Equals("Schedule")
                        || objType.Name.Equals("Drawing Sheet"))
                    {
                        continue;
                    }
                    else
                    {
                        m_allViews.Insert(view);
                        AssortViews(view.Name, objType.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Assort all views for tree view displaying.
        /// </summary>
        /// <param name="view">The view assorting</param>
        /// <param name="type">The type of view</param>
        private void AssortViews(string view, string type)
        {
            foreach (TreeNode t in AllViewsNames.Nodes)
            {
                if (t.Tag.Equals(type))
                {
                    t.Nodes.Add(new TreeNode(view));
                    return;
                }
            }

            TreeNode categoryNode = new TreeNode(type);
            categoryNode.Tag = type;
            if (type.Equals("Building Elevation"))
            {
                categoryNode.Text = "Elevations [" + type + "]";
            }
            else
            {
                categoryNode.Text = type + "s";
            }
            categoryNode.Nodes.Add(new TreeNode(view));
            AllViewsNames.Nodes.Add(categoryNode);
        }

        /// <summary>
        /// Retrieve the checked view from tree view.
        /// </summary>
        public void SelectViews()
        {
            ArrayList names = new ArrayList();
            foreach (TreeNode t in AllViewsNames.Nodes)
            {
                foreach (TreeNode n in t.Nodes)
                {
                    if (n.Checked && 0 == n.Nodes.Count)
                    {
                        names.Add(n.Text);
                    }
                }
            }

            foreach (Autodesk.Revit.DB.View v in m_allViews)
            {
                foreach (string s in names)
                {
                    if (s.Equals(v.Name))
                    {
                        m_selectedViews.Insert(v);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Generate sheet in active document.
        /// </summary>
        /// <param name="doc">the currently active document</param>
        /// public void GenerateSheet(Document doc)
        /// {
        ///     if (null == doc)
        ///     {
        ///         throw new ArgumentNullException("doc");
        ///     }
        /// 
        ///     if (0 == m_selectedViews.Size)
        ///     {
        ///         throw new InvalidOperationException("No view be selected, generate sheet be canceled.");
        ///     }
        ///     ViewSheet sheet = ViewSheet.Create(doc, m_titleBlock.Id);
        ///     sheet.Name = SheetName;
        ///     PlaceViews(m_selectedViews, sheet);
        /// }

        /// <summary>
        /// Generate multiple sheets while iterating through the selected views 
        /// </summary>
        /// <param name="doc"></param>
        public void GenerateSheets(Document doc)
        {
            if (null == doc)
            {
                throw new ArgumentNullException("doc");
            }

            if (0 == m_selectedViews.Size)
            {
                throw new InvalidOperationException("No view be selected, generate sheet be canceled.");
            }

            // Insert loop here to create multiple sheets
            // change main to this function name, rather than GenerateSheet (no 's')
            foreach (Autodesk.Revit.DB.View currentView in m_selectedViews)
            {
                ViewSheet sheet = ViewSheet.Create(doc, m_titleBlock.Id);
                String viewName = currentView.Name;
                //String viewCategory = currentView.Category.CategoryType.ToString();
                String viewType = currentView.ViewType.ToString();
                sheet.Name = SheetName + viewType + " - " + viewName;
                PlaceViews(currentView, sheet);
            }
        }

        /// <summary>
        /// Retrieve the title block to be generate by its name.
        /// </summary>
        /// <param name="name">The title block's name</param>
        public void ChooseTitleBlock(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            foreach (FamilySymbol f in m_allTitleBlocks)
            {
                if (name.Equals(f.Family.Name + ":" + f.Name))
                {
                    m_titleBlock = f;
                    return;
                }
            }
        }

        /// <summary>
        /// Retrieve all available title blocks in the currently active document.
        /// </summary>
        /// <param name="doc">the currently active document</param>
        private void GetTitleBlocks(Document doc)
        {
            FilteredElementCollector filteredElementCollector = new FilteredElementCollector(doc);
            filteredElementCollector.OfClass(typeof(FamilySymbol));
            filteredElementCollector.OfCategory(BuiltInCategory.OST_TitleBlocks);
            m_allTitleBlocks = filteredElementCollector.ToElements();
            if (0 == m_allTitleBlocks.Count)
            {
                throw new InvalidOperationException("There is no title block to generate sheet.");
            }

            foreach (Element element in m_allTitleBlocks)
            {
                FamilySymbol f = element as FamilySymbol;
                AllTitleBlocksNames.Add(f.Family.Name + ":" + f.Name);
                if (null == m_titleBlock)
                {
                    m_titleBlock = f;
                }
            }
        }

        /// <summary>
        /// Place all selected views on this sheet's appropriate location.
        /// </summary>
        /// <param name="views">all selected views</param>
        /// <param name="sheet">all views located sheet</param>
        private void PlaceViews(Autodesk.Revit.DB.View views, ViewSheet sheet)
        {
            double xDistance = 0;
            double yDistance = 0;
            CalculateDistance(sheet.Outline, 1, ref xDistance, ref yDistance);

            Autodesk.Revit.DB.UV origin = GetOffSet(sheet.Outline, xDistance, yDistance);
            //Autodesk.Revit.DB.UV temp = new Autodesk.Revit.DB.UV (origin.U, origin.V);
            double tempU = origin.U;
            double tempV = origin.V;
            int n = 1;

            //foreach (Autodesk.Revit.DB.View v in views)
            Autodesk.Revit.DB.UV location = new Autodesk.Revit.DB.UV(tempU, tempV);
            Autodesk.Revit.DB.View view = views;
            Rescale(view, xDistance, yDistance);
            try
            {
                //sheet.AddView(view, location);
                Viewport.Create(view.Document, sheet.Id, view.Id, new XYZ(location.U, location.V, 0));
                //Viewport.Create(view.Document, sheet.Id, view.Id, new XYZ(0, 0, 0));
            }
            catch (ArgumentException /*ae*/)
            {
                throw new InvalidOperationException("The view '" + view.Name +
                    "' can't be added, it may have already been placed in another sheet.");
            }

            if (0 != n++ % m_rows)
            {
                tempU = tempU + xDistance * (1 - TITLEBAR);
            }
            else
            {
                tempU = origin.U;
                tempV = tempV + yDistance;
            }
        }

        /// <summary>
        /// Retrieve the appropriate origin.
        /// </summary>
        /// <param name="bBox"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private Autodesk.Revit.DB.UV GetOffSet(BoundingBoxUV bBox, double x, double y)
        {
            //return new Autodesk.Revit.DB.UV(bBox.Min.U + x * GOLDENSECTION, bBox.Min.V + y * GOLDENSECTION);
            return new UV(bBox.Max.U * .45, bBox.Max.V / 2);
        }

        /// <summary>
        /// Calculate the appropriate distance between the views lay on the sheet.
        /// </summary>
        /// <param name="bBox">The outline of sheet.</param>
        /// <param name="amount">Amount of views.</param>
        /// <param name="x">Distance in x axis between each view</param>
        /// <param name="y">Distance in y axis between each view</param>
        private void CalculateDistance(BoundingBoxUV bBox, int amount, ref double x, ref double y)
        {
            double xLength = (bBox.Max.U - bBox.Min.U) * (1 - TITLEBAR);
            double yLength = (bBox.Max.V - bBox.Min.V);

            //calculate appropriate rows numbers.
            double result = Math.Sqrt(amount);

            while (0 < (result - (int)result))
            {
                amount = amount + 1;
                result = Math.Sqrt(amount);
            }
            m_rows = result;
            double area = xLength * yLength / amount;

            //calculate appropriate distance between the views.
            if (bBox.Max.U > bBox.Max.V)
            {
                x = Math.Sqrt(area / GOLDENSECTION);
                y = GOLDENSECTION * x;
            }
            else
            {
                y = Math.Sqrt(area / GOLDENSECTION);
                x = GOLDENSECTION * y;
            }
        }

        /// <summary>
        /// Rescale the view's Scale value for suitable.
        /// </summary>
        /// <param name="view">The view to be located on sheet.</param>
        /// <param name="x">Distance in x axis between each view</param>
        /// <param name="y">Distance in y axis between each view</param>
        static private void Rescale(Autodesk.Revit.DB.View view, double x, double y)
        {
            double Rescale = 2;
            Autodesk.Revit.DB.UV outline = new Autodesk.Revit.DB.UV(view.Outline.Max.U - view.Outline.Min.U,
                view.Outline.Max.V - view.Outline.Min.V);

            if (outline.U > outline.V)
            {
                Rescale = outline.U / x * Rescale;
            }
            else
            {
                Rescale = outline.V / y * Rescale;
            }
            if (1 != view.Scale && 0 != Rescale)
            {
                view.Scale = (int)(view.Scale * Rescale);
            }
        }
    }
}
