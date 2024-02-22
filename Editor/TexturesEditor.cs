using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Object = UnityEngine.Object;
using System.IO;
using System;

namespace TexturesEditor
{
    public class TexturesEditor : EditorWindow
    {
        private DropdownField textureOptionDDF;
        private Button createBtn, exportBtn;
        private IntegerField widthField, heightField;
        private ObjectField textureField;
        private GradientField horGradientField, verGradientField;
        private ColorField colorField;
        private VisualElement previewElement, textureValues;
        private ComputeShader computeShader;
        private Label alertBanner;
        private SliderInt radiusSlider;

        private Texture2D selectedTexture, outputTexture;
        private string outputName;


        [MenuItem("Acme Studio/Textures Editor")]
        private static void ShowWindow()
        {
            TexturesEditor texEditorWnd = GetWindow<TexturesEditor>();
            texEditorWnd.titleContent = new GUIContent("Textures Editor");
            texEditorWnd.maxSize = new Vector2(500, 700);
            texEditorWnd.minSize = texEditorWnd.maxSize;
            texEditorWnd.Show();
        }

        private void CreateGUI()
        {
            computeShader = Resources.Load("ComputeShader", typeof(ComputeShader)) as ComputeShader;

            // Build UI
            VisualElement root = rootVisualElement;
            VisualTreeAsset visualTree = Resources.Load("Ui Documents/TexturesEditorWindow", typeof(VisualTreeAsset)) as VisualTreeAsset;
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);

            // Assign References from tree
            textureOptionDDF = root.Q<DropdownField>(name: "Texture-Option");
            createBtn = root.Q<Button>(name: "Create-Texture-Button");
            exportBtn = root.Q<Button>(name: "Export-Button");
            widthField = root.Q<IntegerField>(name: "Width-Field");
            heightField = root.Q<IntegerField>(name: "Height-Field");
            textureField = root.Q<ObjectField>(name: "Texture-Field");
            horGradientField = root.Q<GradientField>(name: "Horizontal-Gradient-Field");
            verGradientField = root.Q<GradientField>(name: "Vertical-Gradient-Field");
            previewElement = root.Q<VisualElement>(name: "Preview-Element");
            textureValues = root.Q<VisualElement>(name: "Texture-Values");
            alertBanner = root.Q<Label>(name: "Alert-Banner");
            radiusSlider = root.Q<SliderInt>(name: "Radius");

            // Register Callbacks
            textureOptionDDF.RegisterValueChangedCallback<string>(TextureOptionSelected);
            textureField.RegisterValueChangedCallback<Object>(TextureSelected);
            horGradientField.RegisterValueChangedCallback<Gradient>(GradientChanged);
            verGradientField.RegisterValueChangedCallback<Gradient>(GradientChanged);
            colorField.RegisterValueChangedCallback<Color>(ColorChanged);
            radiusSlider.RegisterValueChangedCallback<int>(RadiusChanged);

            radiusSlider.highValue = Mathf.Min(widthField.value / 2, heightField.value / 2);

            alertBanner.style.display = DisplayStyle.None;

            exportBtn.clicked += () => ExportTexture(outputTexture);
            createBtn.clicked += CreateTexture;

            previewElement.style.backgroundImage = null;

            TextureOptionSelected(null);
        }

        private void ColorChanged(ChangeEvent<Color> evt)
        {
            ApplyParams();
        }

        private void RadiusChanged(ChangeEvent<int> evt)
        {
            ApplyParams();
        }

        private void GradientChanged(ChangeEvent<Gradient> evt)
        {
            ApplyParams();
        }

        private void TextureSelected(ChangeEvent<Object> textureObj)
        {
            if (textureObj == null || textureObj.newValue == null)
                selectedTexture = null;
            else
            {
                selectedTexture = (Texture2D)textureObj.newValue;
                outputName = textureObj.newValue.name;
            }

            ApplyParams();
        }

        private void TextureOptionSelected(ChangeEvent<string> selection)
        {
            selectedTexture = null;

            if (selection == null || String.IsNullOrEmpty(selection.newValue))
            {
                textureValues.style.display = DisplayStyle.Flex;
                textureField.style.display = DisplayStyle.None;
                createBtn.style.display = DisplayStyle.Flex;
                return;
            }
            switch (selection.newValue)
            {
                default:
                case "Create New Texture":
                    textureValues.style.display = DisplayStyle.Flex;
                    createBtn.style.display = DisplayStyle.Flex;
                    textureField.style.display = DisplayStyle.None;
                    break;
                case "Load Texture":
                    textureField.value = null;
                    textureValues.style.display = DisplayStyle.None;
                    createBtn.style.display = DisplayStyle.None;
                    textureField.style.display = DisplayStyle.Flex;
                    break;
            }
            ApplyParams();
        }

        private void CreateTexture()
        {
            int width = Mathf.Max(10, widthField.value);
            int height = Mathf.Max(10, heightField.value);
            widthField.SetValueWithoutNotify(width);
            heightField.SetValueWithoutNotify(height);
            selectedTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, mipChain: false);
            Color32[] colors = new Color32[width * height];
            Array.Fill(colors, Color.white);
            selectedTexture.SetPixels32(colors);
            selectedTexture.Apply();

            outputName = "New Texture";

            ApplyParams();
        }

        private void ApplyParams()
        {
            if (selectedTexture == null)
            {
                previewElement.style.backgroundImage = null;
                exportBtn.SetEnabled(false);
                return;
            }

            if (!selectedTexture.isReadable)
            {
                previewElement.style.display = DisplayStyle.None;
                alertBanner.style.display = DisplayStyle.Flex;
                exportBtn.SetEnabled(false);
                return;
            }

            radiusSlider.highValue = Mathf.Min(selectedTexture.width / 2, selectedTexture.height / 2);
            previewElement.style.display = DisplayStyle.Flex;
            alertBanner.style.display = DisplayStyle.None;

            ComputeTexture();

            ApplyToPreview();

            exportBtn.SetEnabled(true);
        }

        private void ComputeTexture()
        {
            outputTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBAFloat, mipChain: false);

            RenderTexture resultTexture = new RenderTexture(selectedTexture.width, selectedTexture.height, 0);
            resultTexture.enableRandomWrite = true;
            resultTexture.Create();

            Texture2D horGradientTexture = ConvertGradientToTexture(horGradientField.value, selectedTexture.width);
            Texture2D verGradientTexture = ConvertGradientToTexture(verGradientField.value, selectedTexture.height);

            computeShader.SetTexture(0, "InputTexture", selectedTexture);
            computeShader.SetTexture(0, "HorGradient", horGradientTexture);
            computeShader.SetTexture(0, "VerGradient", verGradientTexture);
            computeShader.SetTexture(0, "ResultTexture", resultTexture);
            computeShader.SetInt("Radius", radiusSlider.value);
            computeShader.SetInt("TexWidth", selectedTexture.width);
            computeShader.SetInt("TexHeight", selectedTexture.height);


            int threadGroupsX = Mathf.CeilToInt(selectedTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(selectedTexture.height / 8.0f);


            computeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

            RenderTexture.active = resultTexture;
            outputTexture.ReadPixels(new Rect(0, 0, resultTexture.width, resultTexture.height), 0, 0);
            outputTexture.Apply();

            RenderTexture.active = null;
        }

        Texture2D ConvertGradientToTexture(Gradient gradient, int texLength)
        {
            Texture2D texture = new Texture2D(texLength, 1, TextureFormat.RGBAFloat, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i < texLength + 1; i++)
            {
                float t = (float)i / texLength;
                Color color = gradient.Evaluate(t);
                texture.SetPixel(i, 0, color);
            }

            texture.Apply();
            return texture;
        }

        private void ExportTexture(Texture2D outputTexture)
        {
            string path = EditorUtility.SaveFilePanel(
                title: "Save Texture",
                directory: Application.dataPath,
                defaultName: String.Concat(outputName + ".png"),
                extension: "png"
            );
            byte[] bytes = outputTexture.EncodeToPNG();

            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllBytes(path, bytes);

            string pathString = path;
            int assetIndex = pathString.IndexOf(value: "Assets", StringComparison.Ordinal);
            string filePath = pathString.Substring(assetIndex, length: path.Length - assetIndex);
            AssetDatabase.ImportAsset(filePath);
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = (Texture2D)AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));
        }

        private void ApplyToPreview()
        {
            bool greaterwidth = outputTexture.width > outputTexture.height;
            float xRatio = 1;
            float yRatio = 1;
            if (greaterwidth)
                yRatio = (float)outputTexture.height / (float)outputTexture.width;
            else
                xRatio = (float)outputTexture.width / (float)outputTexture.height;

            previewElement.style.width = 400 * xRatio;
            previewElement.style.height = 400 * yRatio;

            previewElement.style.backgroundImage = outputTexture;
        }

    }
}



