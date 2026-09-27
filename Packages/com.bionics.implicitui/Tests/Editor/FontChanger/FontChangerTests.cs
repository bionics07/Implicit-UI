using System.Linq;
using ImplicitUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if IMPLICITUI_TMP
using TMPro;
#endif

namespace ImplicitUI.Tests.EditorTests
{
    // The Font Changer changes files in bulk, so every rule that keeps it safe has a test: the dry run finds everything and
    // changes nothing, prefab values are only changed where they are defined, anything that moved since the scan is left
    // alone, and scene changes can be undone. Scene tests run in the Test Runner's own scene; prefab tests write to a
    // temporary folder that is deleted afterwards.
    public class FontChangerTests
    {
        private const string Folder = "Assets/ImplicitUIFontChangerTest";

        private Font m_FontA;
        private Font m_FontB;
        private GameObject m_SceneRoot;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "ImplicitUIFontChangerTest");
            AssetDatabase.CreateFolder(Folder, "Sub");
            m_FontA = UiTestUtility.BuiltinFont();

            // A second font saved as an asset, so prefabs can keep a reference to it.
            m_FontB = new Font("Font B");
            AssetDatabase.CreateAsset(m_FontB, Folder + "/FontB.fontsettings");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_SceneRoot != null)
                Object.DestroyImmediate(m_SceneRoot);

            AssetDatabase.DeleteAsset(Folder);
        }

        // --- Scene mode -----------------------------------------------------------------------------------------------

        [Test]
        public void ScanFindsInactiveTextsAndChangesNothing()
        {
            m_SceneRoot = Node(null, "Menu");
            var active = LegacyText(m_SceneRoot.transform, "Active");
            var hidden = LegacyText(m_SceneRoot.transform, "Hidden");
            hidden.gameObject.SetActive(false);

            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));

            Assert.That(scan.Items.Count, Is.EqualTo(2));
            Assert.That(active.font, Is.SameAs(m_FontA), "the scan is a dry run");
            Assert.That(hidden.font, Is.SameAs(m_FontA));

            // The window lists what each text says, and marks the ones that are not on screen.
            var hiddenItem = scan.Items.Single(item => item.Content == "Hidden");
            Assert.That(hiddenItem.Active, Is.False);
            Assert.That(scan.Items.Single(item => item.Content == "Active").Active, Is.True);
        }

        [Test]
        public void ApplyChangesTheCheckedTextsOnly()
        {
            m_SceneRoot = Node(null, "Menu");
            var first = LegacyText(m_SceneRoot.transform, "Label");
            var second = LegacyText(m_SceneRoot.transform, "Label");
            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));

            // Same name, different positions: only the second one is checked.
            scan.Items.First(item => item.ChildPath.Last() == 0).Selected = false;
            FontChangeApplier.Apply(scan);

            Assert.That(first.font, Is.SameAs(m_FontA));
            Assert.That(second.font, Is.SameAs(m_FontB));
        }

        [Test]
        public void FromFontLeavesOtherFontsAlone()
        {
            m_SceneRoot = Node(null, "Menu");
            LegacyText(m_SceneRoot.transform, "A");
            var already = LegacyText(m_SceneRoot.transform, "B");
            already.font = m_FontB;
            var settings = SceneSettings(m_FontA);
            settings.FromFont = m_FontB;

            var scan = FontChangeScanner.Scan(settings);

            Assert.That(scan.Items.Count, Is.EqualTo(1));
            Assert.That(scan.Items[0].CurrentFont, Is.SameAs(m_FontB));
        }

        [Test]
        public void TextAlreadyOnTheTargetFontIsNotListed()
        {
            m_SceneRoot = Node(null, "Menu");
            LegacyText(m_SceneRoot.transform, "Done").font = m_FontB;

            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));

            Assert.That(scan.Items, Is.Empty);
        }

        [Test]
        public void SceneChangesCanBeUndone()
        {
            m_SceneRoot = Node(null, "Menu");
            var text = LegacyText(m_SceneRoot.transform, "Label");
            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));
            Undo.IncrementCurrentGroup();

            FontChangeApplier.Apply(scan);
            Assert.That(text.font, Is.SameAs(m_FontB));

            Undo.PerformUndo();
            Assert.That(text.font, Is.SameAs(m_FontA));
        }

        [Test]
        public void TextChangedAfterTheScanIsSkipped()
        {
            m_SceneRoot = Node(null, "Menu");
            var text = LegacyText(m_SceneRoot.transform, "Label");
            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));
            var other = new Font("Other");
            text.font = other;

            var log = FontChangeApplier.Apply(scan);

            Assert.That(text.font, Is.SameAs(other));
            Assert.That(log.Single().Changed, Is.False);
            Object.DestroyImmediate(other);
        }

        // --- Prefabs --------------------------------------------------------------------------------------------------

        [Test]
        public void InstanceTakingTheFontFromItsPrefabIsSkipped()
        {
            var prefab = SavePrefab("Button", LegacyText(null, "Label").gameObject);
            m_SceneRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));
            FontChangeApplier.Apply(scan);

            var item = scan.Items.Single();
            Assert.That(item.Reason, Is.EqualTo(FontChangeReason.Inherited));
            Assert.That(item.InheritedFrom, Is.EqualTo(AssetDatabase.GetAssetPath(prefab)));
            Assert.That(m_SceneRoot.GetComponent<Text>().font, Is.SameAs(m_FontA));
            Assert.That(PrefabUtility.GetPropertyModifications(m_SceneRoot).Any(m => m.propertyPath == FontKinds.LegacyFontProperty),
                Is.False, "no override was created");
        }

        [Test]
        public void InstanceThatAlreadyOverridesTheFontIsChanged()
        {
            var prefab = SavePrefab("Button", LegacyText(null, "Label").gameObject);
            m_SceneRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var other = AssetDatabase.LoadAssetAtPath<Font>(CreateFontAsset("Other"));
            var text = m_SceneRoot.GetComponent<Text>();
            text.font = other;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);

            var scan = FontChangeScanner.Scan(SceneSettings(m_FontB));
            FontChangeApplier.Apply(scan);

            Assert.That(scan.Items.Single().Reason, Is.EqualTo(FontChangeReason.WillChange));
            Assert.That(text.font, Is.SameAs(m_FontB));
            Assert.That(prefab.GetComponent<Text>().font, Is.SameAs(m_FontA), "the prefab asset is untouched");
        }

        [Test]
        public void FolderModeChangesAndSavesPrefabs()
        {
            var path = AssetDatabase.GetAssetPath(SavePrefab("Button", LegacyText(null, "Label").gameObject));

            var scan = FontChangeScanner.Scan(FolderSettings(m_FontB, true));
            var log = FontChangeApplier.Apply(scan);

            Assert.That(log.Single().Changed, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Text>().font, Is.SameAs(m_FontB));
        }

        [Test]
        public void VariantTakingTheFontFromItsBaseIsSkipped()
        {
            var basePrefab = SavePrefab("Base", LegacyText(null, "Label").gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var variantPath = Folder + "/Variant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);

            var scan = FontChangeScanner.Scan(FolderSettings(m_FontB, true));

            var variantItem = scan.Items.Single(item => item.AssetPath == variantPath);
            Assert.That(variantItem.Reason, Is.EqualTo(FontChangeReason.Inherited));
            Assert.That(scan.Items.Single(item => item.AssetPath != variantPath).Reason,
                Is.EqualTo(FontChangeReason.WillChange));
        }

        [Test]
        public void NestedPrefabIsChangedInItsOwnAsset()
        {
            var inner = SavePrefab("Inner", LegacyText(null, "Label").gameObject);
            var outerRoot = Node(null, "Outer");
            PrefabUtility.InstantiatePrefab(inner, outerRoot.transform);
            var outerPath = AssetDatabase.GetAssetPath(SavePrefab("Outer", outerRoot));

            var scan = FontChangeScanner.Scan(FolderSettings(m_FontB, true));

            Assert.That(scan.Items.Single(item => item.AssetPath == outerPath).Reason, Is.EqualTo(FontChangeReason.Inherited));
        }

        [Test]
        public void SubfoldersAreLeftOutWhenAskedTo()
        {
            SavePrefab("Top", LegacyText(null, "Label").gameObject);
            SavePrefab("Sub/Deep", LegacyText(null, "Label").gameObject);

            Assert.That(FontChangeScanner.Scan(FolderSettings(m_FontB, false)).Items.Count, Is.EqualTo(1));
            Assert.That(FontChangeScanner.Scan(FolderSettings(m_FontB, true)).Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void FolderOutsideAssetsIsRefused()
        {
            var settings = FolderSettings(m_FontB, true);
            settings.Folder = "Packages/com.bionics.implicitui";

            Assert.That(settings.Validate(), Is.Not.Empty);
        }

#if IMPLICITUI_TMP
        [Test]
        public void MaterialMadeForAnotherFontBlocksTheRun()
        {
            var font = RequireTmpFont();
            var foreign = new Material(font.material);
            var otherAtlas = new Texture2D(4, 4);
            foreign.SetTexture(ShaderUtilities.ID_MainTex, otherAtlas);
            var settings = new FontChangeSettings { Kind = FontKind.TextMeshPro, ToFont = font, ToMaterial = foreign };

            Assert.That(settings.Validate(), Is.Not.Empty);
            settings.ToMaterial = font.material;
            Assert.That(settings.Validate(), Is.Empty);

            Object.DestroyImmediate(foreign);
            Object.DestroyImmediate(otherAtlas);
        }

        [Test]
        public void TextWithItsOwnMaterialIsUncheckedUnlessFiltered()
        {
            var font = RequireTmpFont();
            var fontCopy = CreateTmpFontCopy(font);
            var preset = new Material(font.material) { name = "Outline preset" };
            AssetDatabase.CreateAsset(preset, Folder + "/Preset.mat");
            m_SceneRoot = Node(null, "Menu");
            var text = m_SceneRoot.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = preset;

            var settings = new FontChangeSettings { Kind = FontKind.TextMeshPro, ToFont = fontCopy };
            var item = FontChangeScanner.Scan(settings).Items.Single();
            Assert.That(item.Reason, Is.EqualTo(FontChangeReason.ReplacesCustomMaterial));
            Assert.That(item.Selected, Is.False);

            settings.FromMaterial = preset;
            Assert.That(FontChangeScanner.Scan(settings).Items.Single().Selected, Is.True);
        }

        [Test]
        public void ChangedTextGetsTheTargetFontsMaterial()
        {
            var font = RequireTmpFont();
            var fontCopy = CreateTmpFontCopy(font);
            m_SceneRoot = Node(null, "Menu");
            var text = m_SceneRoot.AddComponent<TextMeshProUGUI>();
            text.font = font;

            var scan = FontChangeScanner.Scan(new FontChangeSettings { Kind = FontKind.TextMeshPro, ToFont = fontCopy });
            FontChangeApplier.Apply(scan);

            Assert.That(text.font, Is.SameAs(fontCopy));

            // Equal, not same: after SaveAssets Unity can hand out a new C# wrapper for the same material asset.
            Assert.That(text.fontSharedMaterial, Is.EqualTo(fontCopy.material));
        }

        private static TMP_FontAsset RequireTmpFont()
        {
            TMP_FontAsset font = null;
            try
            {
                font = TMP_Settings.defaultFontAsset;
            }
            catch (System.NullReferenceException)
            {
            }

            if (font == null)
                Assert.Ignore("No default TextMesh Pro font: import TMP Essential Resources to run this test.");
            return font;
        }

        // A second font asset on disk, with its own material asset, like a font imported next to the default one.
        private static TMP_FontAsset CreateTmpFontCopy(TMP_FontAsset font)
        {
            var copy = Object.Instantiate(font);
            copy.name = "Copy SDF";
            AssetDatabase.CreateAsset(copy, Folder + "/Copy SDF.asset");
            var material = new Material(font.material) { name = "Copy SDF Material" };
            AssetDatabase.AddObjectToAsset(material, copy);
            copy.material = material;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }
#endif

        private static FontChangeSettings SceneSettings(Font to)
        {
            return new FontChangeSettings { Kind = FontKind.LegacyText, Mode = FontChangeMode.Scene, ToFont = to };
        }

        private static FontChangeSettings FolderSettings(Font to, bool subfolders)
        {
            return new FontChangeSettings
            {
                Kind = FontKind.LegacyText, Mode = FontChangeMode.Folder, ToFont = to, Folder = Folder,
                IncludeSubfolders = subfolders,
            };
        }

        private static string CreateFontAsset(string name)
        {
            var path = Folder + "/" + name + ".fontsettings";
            AssetDatabase.CreateAsset(new Font(name), path);
            return path;
        }

        // Saves the object as a prefab and destroys the scene copy.
        private static GameObject SavePrefab(string name, GameObject source)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/" + name + ".prefab");
            Object.DestroyImmediate(source);
            return prefab;
        }

        private Text LegacyText(Transform parent, string name)
        {
            var text = Node(parent, name).AddComponent<Text>();
            text.font = m_FontA;
            text.text = name;
            return text;
        }

        private static GameObject Node(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                node.transform.SetParent(parent, false);
            return node;
        }
    }
}
