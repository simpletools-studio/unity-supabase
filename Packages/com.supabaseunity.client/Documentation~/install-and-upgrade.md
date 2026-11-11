# Install and upgrade

## Choose an installation route

For Git or tarball installs, use Package Manager. Select **Add package from git URL** and paste the versioned URL from the [package README](../README.md), or select **Add package from tarball** and choose the downloaded `.tgz`. Unity resolves Json.NET from the package manifest.

For an Assets import, install `com.unity.nuget.newtonsoft-json` 3.2.1 or newer, then choose **Assets > Import Package > Custom Package** and import the release's `.unitypackage`. The import creates `Assets/SupabaseUnity`.

Install one distribution in each Unity project. A UPM install and an Assets import contain assemblies with the same names.

## Upgrade an existing project

1. Save your scenes and create a version-control checkpoint or backup with the `.meta` files. Keep project settings assets and handwritten code in your own folders under `Assets`.
2. For Git, replace the existing version tag with the new release tag. For a tarball install, select the new `.tgz` in Package Manager. For an Assets install, import the newer `.unitypackage` into the existing project.
3. After Unity finishes compiling, open **Tools > Supabase > Setup** and select your existing settings asset. Confirm its URL, schema and runtime options, then run **Test Project Connection**.
4. Open the scene or prefab that owns your client and confirm it still references the settings asset.
5. If you use IL2CPP, regenerate models created before 0.8.0 or preserve the reflected properties manually. Review the preview before applying updates. [Model preservation](api.md#model-preservation-in-il2cpp-builds)

## Update the Quickstart sample

Package Manager copies imported samples into `Assets`. Updating the package leaves those project files in place. Keep your working scene and settings references if you only need the SDK update.

To refresh the sample, save any customizations before replacing the previous import. Keep one compiled copy of `SupabaseQuickstart.cs` and its matching scene. Check the settings reference in the refreshed scene and reassign it if needed. The Assets distribution includes the sample under `Assets/SupabaseUnity/Samples/Quickstart`.

## Switch distributions or recover from duplicate assemblies

Back up your settings, scenes, models, and their `.meta` files first. Remove the existing UPM dependency in Package Manager or remove the package-owned Assets import. Move any user-created assets out of the import folder before removing it. Install the desired distribution, allow Unity to compile, and check the references in your scenes and prefabs.

Inspect the first compiler error if Unity reports duplicate assembly or type names. Check both **Packages** and `Assets/SupabaseUnity` for a second SDK copy, and check `Assets/Samples` for duplicate Quickstart scripts.

<a id="turkce-kurulum-ve-yukseltme"></a>

## Türkçe kurulum ve yükseltme

Git bağlantısını veya `.tgz` dosyasını Package Manager üzerinden ekle. UPM, Json.NET bağımlılığını çözer. `.unitypackage` kullanıyorsan önce `com.unity.nuget.newtonsoft-json` 3.2.1 veya daha yeni bir sürüm kur. Ardından **Assets > Import Package > Custom Package** seçeneğiyle paketi içe aktar.

Yükseltmeden önce sahneleri kaydet ve `.meta` dosyalarıyla birlikte bir yedek veya sürüm kontrolü kaydı oluştur. Git etiketini değiştir, yeni `.tgz` dosyasını ekle veya yeni `.unitypackage` dosyasını mevcut projeye aktar. Unity derlemeyi bitirdiğinde **Tools > Supabase > Setup** ekranında mevcut ayar dosyanı seç. Sahne ve prefab bağlantılarını kontrol et.

IL2CPP kullanıyorsan 0.8.0 öncesinde üretilen modelleri yeniden üret veya özelliklerine koruma özniteliklerini ekle. Değişiklikleri yazmadan önce önizlemeyi incele. [Model koruma rehberi](api.md#model-preservation-in-il2cpp-builds)

Quickstart örneğini yenilemeden önce özelleştirmelerini yedekle. Tek bir `SupabaseQuickstart.cs` kopyası bırak ve yeni sahnede ayar dosyası bağlantısını kontrol et. Dağıtım biçimini değiştirirken önce eski SDK kopyasını kaldır. Ayar dosyalarını ve kendi kodunu paket klasöründen ayırıp koru.
