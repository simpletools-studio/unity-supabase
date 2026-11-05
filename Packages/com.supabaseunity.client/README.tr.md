# Unity için Supabase

Bu paket Unity 2021.3 LTS ile Unity 6 arasında aynı API ile Auth, Database, Realtime, Storage ve Edge Functions kullanmak için Unity’ye özgü bir istemci sağlar. WebGL’de tarayıcı WebSocket köprüsünü kullanır; diğer platformlarda native WebSocket kullanır. Oturum kalıcılığı varsayılan olarak kapalıdır. Açıldığında oturumlar WebGL’de `localStorage`, diğer platformlarda `persistentDataPath` altında saklanır.

> `0.7.0`, Database arama ve dizi filtrelerini ve Storage listeleme seçeneklerini düzeltir.
> Yerel servis testleri ve sınırlar için [servis uyumluluğu](Documentation~/service-compatibility.md) rehberine bakın.

## Hızlı başlangıç

Package Manager’da **Add package from git URL** seçeneğine şunu gir:

```text
https://github.com/DorukTan/unity-supabase.git?path=/Packages/com.supabaseunity.client#v0.7.0
```

Ardından **Tools > Supabase > Setup** ekranını açıp ayar dosyasını oluştur. Supabase Dashboard’daki **Connect** ekranından proje URL’sini ve `sb_publishable_...` anahtarını al, sonra **Test Project Connection** düğmesine bas. Bu test bağlantıyı ve anahtarı kontrol eder; RLS kurallarını değiştirmez. Eski `anon` JWT anahtarları da desteklenir.

Seçilen ayar dosyasının yolu Setup ekranında gösterilir. Oturum kalıcılığı, otomatik token
yenileme, Realtime başlangıcı ve istek zaman aşımı ayarları, Setup ve Inspector içindeki
**Runtime options** bölümündedir. Oturum kalıcılığı açıksa, bu bölüm kapalıyken de depolama
uyarısı görünür. Ayarları değiştirmek önceki bağlantı sonucunu temizler.

```csharp
using Supabase.Unity;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
    [SerializeField] private SupabaseSettings settings;
    private SupabaseClient client;

    private async void Start()
    {
        client = new SupabaseClient(settings);
        var initialized = await client.InitializeAsync();
        if (!initialized.IsSuccess)
        {
            Debug.LogError(initialized.Error);
            return;
        }

        var result = await client.From<PlayerProfile>("profiles")
            .Select("id,username,score")
            .Order("score", ascending: false)
            .Limit(20)
            .GetAsync();

        if (result.IsSuccess)
            foreach (var profile in result.Data) Debug.Log(profile.Username);
    }

    private void OnDestroy()
    {
        if (client != null) client.Dispose();
    }
}

public sealed class PlayerProfile
{
    [SupabaseColumn("id")] public string Id { get; set; }
    [SupabaseColumn("username")] public string Username { get; set; }
    [SupabaseColumn("score")] public int Score { get; set; }
}
```

Giriş örneği:

```csharp
var result = await client.Auth.SignInWithPasswordAsync(email, password);
if (!result.IsSuccess)
    Debug.LogError(result.Error.Message);
```

Realtime örneği:

```csharp
var channel = client.Realtime.Channel("scores");
channel.OnPostgresChanges(new RealtimePostgresChangeFilter
{
    Event = RealtimePostgresEvent.All,
    Schema = "public",
    Table = "scores"
}, change => Debug.Log(change.Event + ": " + change.Schema + "." + change.Table));

await channel.SubscribeAsync();
```

Önemli: Unity uygulamasına hiçbir zaman `sb_secret_...` veya `service_role` anahtarı koyma. Bunlar derlenen oyuncu tarafından çıkarılabilir. İstemci güvenliği publishable/anon anahtarını saklamaya değil, Supabase RLS ve Storage policy kurallarına dayanır. Paket bu anahtarları ayar dosyasında fark ederse build’i durdurur.

UPM kurulumu kullanıyorsan Package Manager’daki **Samples** bölümünden **Quickstart** örneğini
içe aktar. `.unitypackage` kurulumu aynı örneği `Assets/SupabaseUnity/Samples/Quickstart`
altında içerir. Setup ekranındaki **Show setup.sql** düğmesiyle SQL dosyasını bul ve içeriğini
Supabase SQL Editor’da çalıştır. **Open Quickstart Scene** ile sahneyi aç, Quickstart nesnesine
ayar dosyasını ata ve Play moduna geç. Sahne açılırken Unity, kaydedilmemiş sahne değişikliklerini
sorar; **Cancel** mevcut sahneyi açık bırakır. Play modunda sahne açma düğmesi devre dışıdır.

Eski bir örnek kopyasında sahne yoksa `SupabaseQuickstart` bileşenini bir GameObject’e ekleyip
ayar dosyasını atayabilirsin. Birden çok örnek kopyası varsa Setup, kullandığı yolu gösterir.
Bir sorun yaşarsan [hata rehberinden](Documentation~/troubleshooting.md) başlayabilirsin.

## C# modelleri oluşturma

1. **Tools > Supabase > Model Generator** ekranını aç. Setup üzerinden açarsan orada seçili ayar dosyasının proje URL'si kullanılır.
2. **Secret Key (Editor only)** alanına Supabase gizli API anahtarını gir. `Assets` altında bir çıktı klasörü ve bir ad alanı seç. İstek, Data API'nin varsayılan şemasını kullanır.
3. **Fetch Schema and Preview** düğmesine bas. **Model File** listesinden bir dosya seçip önerilen kodu incele. Güncellemelerde **Current Source** bölümünden mevcut kodu karşılaştır.
4. Önizlemedeki değişiklikleri yazmak için **Apply Changes** düğmesine bas. Araç Unity meta dosyalarını korur, içeriği aynı olan dosyaları atlar ve şemadan kaldırılan tabloların dosyalarını silmez.

Hedefte elle yazılmış bir dosya varsa dosyayı taşı veya yeniden adlandır ve şemayı tekrar getir.
İnceleme sırasında bir hedef dosya değişirse uygulamadan önce yeni önizleme al. Gizli anahtar istek
tamamlandığında temizlenir ve ayar dosyasına kaydedilmez.

## Play modu dışında Editor betikleri

`Editor` klasöründeki araçlarda `Supabase.Unity.Editor.SupabaseEditorClient.Create(settings)`
ile istemci oluşturabilirsin. Database, Storage, Functions ve doğrudan Auth HTTP çağrıları
Editor güncellemeleriyle ilerler. `await` kullan ve araç penceresinin `OnDisable` metodunda
istemciyi `Dispose()` ile kapat.

Bu istemciler oturum ve PKCE verilerini yalnızca bellekte tutar; otomatik token yenileme,
Realtime ve otomatik tarayıcı girişi desteklenmez. Derleme yenilenirken, Editor kapanırken
veya Play moduna girerken bekleyen istekler iptal edilir. Örnek pencere ve ayrıntılar için
[Editor betikleri rehberine](Documentation~/editor-scripting.md) bakabilirsin.
