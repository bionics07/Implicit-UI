# -*- coding: utf-8 -*-
"""
Monta a copia do package no formato que a Asset Store precisa.

    python .ci/make-assetstore-copy.py [--dest <pasta>] [--editor <versao>] [--verify]

A Asset Store entrega um .unitypackage, que despeja tudo em Assets/ do comprador. La nao existe
`testables` nem `Samples~`, entao a pasta que sobe e diferente da que vai para o UPM:

  1. copia o package para <dest>/Assets/ImplicitUI
  2. deixa a pasta Tests de fora - senao as fixtures do package aparecem no Test Runner do comprador
  3. renomeia Samples~ para Samples - o "~" e o que faz o Package Manager tratar a pasta como sample
     importavel, mas dentro de Assets/ ele so esconde a pasta, e o comprador ficaria sem as demo
     scenes que as Submission Guidelines exigem
  4. preserva todos os .meta, para o GUID de cada script seguir igual entre versoes: e isso que impede
     que um comprador que atualiza perca as referencias em cenas e prefabs

O upload em si continua na mao, pelo Asset Store Publishing Tools dentro desse projeto.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys

PACKAGE = os.path.join("Packages", "com.bionics.implicitui")

# Os modulos que qualquer projeto real tem. Sem eles o projeto de upload nao compila os samples: o
# uGUI vive no modulo ui, e GUIStyle no de IMGUI. So entram modulos que existem em TODAS as versoes
# suportadas - um modulo inexistente derruba a resolucao de pacotes antes de o editor abrir.
MODULES = [
    "ai", "androidjni", "animation", "assetbundle", "audio", "cloth", "director", "imageconversion",
    "imgui", "jsonserialize", "particlesystem", "physics", "physics2d", "screencapture", "terrain",
    "terrainphysics", "tilemap", "ui", "uielements", "umbra", "unityanalytics", "unitywebrequest",
    "unitywebrequestassetbundle", "unitywebrequestaudio", "unitywebrequesttexture",
    "unitywebrequestwww", "vehicles", "video", "wind", "xr",
]

# O uGUI e dependencia do package e nao e modulo, entao entra pelo nome.
UGUI = "1.0.0"

# GUID fixo para as duas pastas que so existem na copia. Deixar a Unity gerar faria o GUID mudar a
# cada release, e um GUID de pasta que muda e ruido desnecessario para quem atualiza.
FOLDER_GUIDS = {
    "": "2b1f4c8e5d7a49c3b8e6f1a4d2c90b75",          # Assets/ImplicitUI
    "Samples": "9e3d70a1c6b84f52ae0d5b83f14c26d9",   # Assets/ImplicitUI/Samples
}
FOLDER_META = ("fileFormatVersion: 2\nguid: {}\nfolderAsset: yes\nDefaultImporter:\n"
               "  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

CHECK_SCRIPT = """using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Temporario, escrito pelo make-assetstore-copy.py --verify.
public static class AssetStoreCopyCheck
{
    public static void Run()
    {
        var failed = false;
        var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
            .Select(a => a.name).Where(n => n.Contains("ImplicitUI")).OrderBy(n => n).ToArray();
        Debug.Log("CHECK: assemblies = " + string.Join(", ", assemblies));

        if (assemblies.Any(n => n.Contains("Tests")))
        {
            Debug.LogError("CHECK: uma assembly de teste foi parar na copia");
            failed = true;
        }

        if (!assemblies.Contains("ImplicitUI.Runtime") || !assemblies.Contains("ImplicitUI.Editor"))
        {
            Debug.LogError("CHECK: a copia nao compilou as assemblies do package");
            EditorApplication.Exit(1);
            return;
        }

        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/ImplicitUI/Samples" });
        Debug.Log("CHECK: cenas de sample encontradas = " + scenes.Length);
        if (scenes.Length == 0)
        {
            Debug.LogError("CHECK: nenhuma demo scene - as Submission Guidelines exigem uma");
            failed = true;
        }

        // O shader do grayscale precisa existir na copia, senao o componente so funciona por Shader.Find.
        if (Shader.Find("Bionics/ImplicitUI/Grayscale") == null)
        {
            Debug.LogError("CHECK: o shader do grayscale nao veio na copia");
            failed = true;
        }

        EditorApplication.Exit(failed ? 1 : 0);
    }
}
"""


def copy_package(dest_assets):
    target = os.path.join(dest_assets, "ImplicitUI")
    if os.path.isdir(target):
        shutil.rmtree(target)

    # A pasta Tests nao vai, e Samples~ chega como Samples.
    shutil.copytree(PACKAGE, target, ignore=shutil.ignore_patterns("Tests", "Tests.meta"))

    hidden = os.path.join(target, "Samples~")
    if os.path.isdir(hidden):
        os.rename(hidden, os.path.join(target, "Samples"))

    for relative, guid in FOLDER_GUIDS.items():
        meta = (os.path.join(target, relative) if relative else target) + ".meta"
        with open(meta, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(FOLDER_META.format(guid))

    return target


def ensure_project(dest, editor):
    assets = os.path.join(dest, "Assets")
    packages = os.path.join(dest, "Packages")
    settings = os.path.join(dest, "ProjectSettings")
    for folder in (assets, packages, settings):
        os.makedirs(folder, exist_ok=True)

    version_file = os.path.join(settings, "ProjectVersion.txt")
    if not os.path.exists(version_file):
        with open(version_file, "w", encoding="utf-8", newline="\n") as handle:
            handle.write("m_EditorVersion: " + editor + "\n")

    manifest_path = os.path.join(packages, "manifest.json")
    manifest = {"dependencies": {}}
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as handle:
            manifest = json.load(handle)

    # Dentro de Assets/ o package.json nao vale, entao a dependencia do uGUI tem que estar no projeto.
    manifest.setdefault("dependencies", {})["com.unity.ugui"] = UGUI
    for module in MODULES:
        manifest["dependencies"].setdefault("com.unity.modules." + module, "1.0.0")

    # O projeto e gerado por este script, entao a lista acima manda: um modulo que saiu dela (porque
    # alguma versao da Unity o removeu) tem que sair do manifest tambem.
    wanted = {"com.unity.modules." + module for module in MODULES}
    for name in [k for k in manifest["dependencies"] if k.startswith("com.unity.modules.")]:
        if name not in wanted:
            manifest["dependencies"].pop(name)
            print("  removido do manifest do projeto de upload: " + name)
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")


def audit(target):
    problems = []
    files = []
    for dirpath, dirnames, filenames in os.walk(target):
        for name in dirnames:
            if name == "Tests" or name.endswith("~"):
                problems.append("pasta que nao deveria estar na copia: " + os.path.join(dirpath, name))
        for name in filenames:
            files.append(os.path.join(dirpath, name))

    donation = re.compile(r"donat|sponsor|ko-?fi|patreon|buymeacoffee", re.IGNORECASE)
    for path in files:
        if os.path.splitext(path)[1].lower() not in (".cs", ".md", ".json", ".asmdef", ".txt", ".shader"):
            continue
        with open(path, encoding="utf-8", errors="replace") as handle:
            if donation.search(handle.read()):
                problems.append("mencao a doacao (proibida no package, 4.9.1.3): " + path)

    scenes = [f for f in files if f.endswith(".unity")]
    if not scenes:
        problems.append("nenhuma demo scene na copia")

    size = sum(os.path.getsize(f) for f in files)
    return files, scenes, size, problems


def verify(dest, editor):
    editor_dir = os.path.join(dest, "Assets", "Editor")
    os.makedirs(editor_dir, exist_ok=True)
    script = os.path.join(editor_dir, "AssetStoreCopyCheck.cs")
    with open(script, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(CHECK_SCRIPT)

    exe = os.path.join(r"C:\Program Files\Unity\Hub\Editor", editor, "Editor", "Unity.exe")
    if not os.path.exists(exe):
        exe = os.path.join(r"D:\UnityEditors", editor, "Editor", "Unity.exe")
    if not os.path.exists(exe):
        print("ERRO: nao achei o editor " + editor)
        return 2

    log = os.path.join(dest, "assetstore-check.log")
    code = subprocess.call([exe, "-batchmode", "-nographics", "-projectPath", dest,
                            "-executeMethod", "AssetStoreCopyCheck.Run", "-logFile", log])

    with open(log, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if line.startswith("CHECK:") or "error CS" in line:
                print("   " + line.rstrip()[:200])

    for leftover in (script, script + ".meta"):
        if os.path.exists(leftover):
            os.remove(leftover)

    print("verificacao na Unity: " + ("OK" if code == 0 else "FALHOU (exit %d)" % code))
    return code


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dest", default=os.path.join("Builds", "assetstore"))
    parser.add_argument("--editor", default="6000.6.0f1",
                        help="A Asset Store Publishing Tools exige 2022.3 ou mais novo.")
    parser.add_argument("--verify", action="store_true",
                        help="Abre o projeto na Unity e confirma que compila e que os samples vieram.")
    args = parser.parse_args()

    if not os.path.isdir(PACKAGE):
        print("ERRO: rode a partir da raiz do repositorio.")
        return 1

    ensure_project(args.dest, args.editor)
    target = copy_package(os.path.join(args.dest, "Assets"))
    files, scenes, size, problems = audit(target)

    print("copia pronta em " + target)
    print("  arquivos: %d  |  tamanho: %.1f KB  |  demo scenes: %d" % (len(files), size / 1024.0, len(scenes)))
    print("  Tests: fora  |  Samples: visivel  |  .meta preservados")

    for problem in problems:
        print("  PROBLEMA: " + problem)

    code = 1 if problems else 0
    if args.verify and not problems:
        code = verify(args.dest, args.editor)

    if code == 0:
        print("")
        print("Proximo passo, na mao: abrir esse projeto na Unity, adicionar a Asset Store Publishing")
        print("Tools pelo Package Manager (My Assets) e subir a pasta Assets/ImplicitUI pelo Uploader.")
    return code


if __name__ == "__main__":
    sys.exit(main())
