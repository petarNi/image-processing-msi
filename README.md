# MMS2026 – Klijent-server sistem za obradu slika

## 1. Opis projekta

Projekat predstavlja klijent-server sistem za obradu slika razvijen u okviru predmeta **Multimedijalni sistemi**.

Sistem omogućava učitavanje slika, primenu filterskih operacija na serverskoj strani, prikaz rezultata u klijentskoj aplikaciji, izvoz u standardne formate i rad sa sopstvenim binarnim formatom slike `.msi`.

Projekat se sastoji iz četiri glavna dela:

* `MMS.Server` – ASP.NET Core Web API server za obradu slika.
* `MMS.Client` – WPF desktop aplikacija za korisnički interfejs.
* `MMS.Server.Tests` – xUnit testovi za serversku logiku i MSI format.
* `MMS.Benchmark` – konzolna aplikacija za merenje performansi.

---

## 2. Korišćene tehnologije

* C#
* .NET 8
* ASP.NET Core Web API
* WPF
* SixLabors.ImageSharp
* Swagger / Swashbuckle
* xUnit
* CSV benchmark izveštaji

---

## 3. Arhitektura sistema

Sistem koristi klijent-server arhitekturu.

Korisnik u WPF klijentu učitava sliku i bira filter. Klijent zatim šalje sliku serveru preko HTTP zahteva. Server obrađuje sliku i vraća rezultat kao PNG. Klijent prikazuje originalnu i obrađenu sliku radi poređenja.

Tok rada:

```text
Korisnik
  ↓
WPF klijent
  ↓ HTTP multipart/form-data
ASP.NET Core server
  ↓
ImageProcessingService / MsiImageService
  ↓
Rezultat kao PNG ili MSI
  ↓
WPF klijent prikazuje/čuva rezultat
```

---

## 4. Server

Server je implementiran kao ASP.NET Core Web API aplikacija.

Glavni delovi servera:

```text
MMS.Server
├── Controllers
│   └── ImageController.cs
├── Helpers
│   └── ImageValidationHelper.cs
├── Models
│   ├── FilterOperation.cs
│   └── ImageUploadRequest.cs
├── Services
│   ├── ImageProcessingService.cs
│   └── Msi
│       ├── Crc32.cs
│       ├── HuffmanCompression.cs
│       ├── MsiHeader.cs
│       └── MsiImageService.cs
└── Program.cs
```

Server omogućava:

* prihvatanje slike preko upload-a,
* validaciju fajla,
* primenu pojedinačnih filtera,
* batch primenu više filtera,
* izvoz u `.msi`,
* uvoz iz `.msi`,
* logovanje zahteva,
* obradu grešaka bez rušenja aplikacije.

---

## 5. API endpoint-i

Server poseduje sledeće endpoint-e:

```http
GET  /api/Image/test
POST /api/Image/invert
POST /api/Image/brightness
POST /api/Image/pixelate
POST /api/Image/emboss-laplacian
POST /api/Image/edge-enhance
POST /api/Image/time-warp
POST /api/Image/jarvis-judice-ninke
POST /api/Image/colorize-grayscale
POST /api/Image/batch
POST /api/Image/export-msi
POST /api/Image/import-msi
```

Svi filter endpoint-i koriste `multipart/form-data`.

Primer za `brightness`:

```text
image  = uploadovana slika
amount = intenzitet osvetljenja
```

Primer za `pixelate`:

```text
image     = uploadovana slika
blockSize = veličina bloka
```

Primer za `time-warp`:

```text
image  = uploadovana slika
factor = intenzitet deformacije
```

---

## 6. Filteri

Pošto se broj indeksa završava cifrom **9**, implementirani su sledeći filteri:

| Filter              | Opis                                             |
| ------------------- | ------------------------------------------------ |
| Invert              | Inverzija RGB komponenti                         |
| Brightness          | Promena osvetljenja slike                        |
| Pixelate            | Pikselizacija slike blokovima                    |
| Emboss Laplacian    | Detekcija ivica Laplasovim kernelom              |
| Edge Enhance        | Naglašavanje ivica                               |
| Time Warp           | Talasna deformacija slike                        |
| Jarvis-Judice-Ninke | Dithering algoritam sa difuzijom greške          |
| Colorize Grayscale  | Kolorizacija slike na osnovu grayscale vrednosti |

---

## 7. Batch obrada

Server podržava batch obradu, odnosno primenu više filtera u jednom pozivu.

Primer:

```json
{"name":"brightness","amount":50}
{"name":"invert"}
{"name":"pixelate","blockSize":12}
```

Filteri se izvršavaju redom, jedan za drugim.

---

## 8. Validacija slika

Server proverava:

* da li je slika poslata,
* veličinu fajla,
* ekstenziju fajla,
* MIME tip,
* validnost imena fajla.

Podržani formati:

```text
PNG
JPG
JPEG
BMP
GIF
```

Maksimalna veličina upload-a:

```text
20 MB
```

---

## 9. MSI format

Projekat implementira sopstveni binarni format slike:

```text
.msi = Multimedia Systems Image
```

Ovaj format nije Windows Installer `.msi`, već prilagođeni format slike napravljen za projekat.

MSI format sadrži:

* magic number,
* verziju,
* zaglavlje,
* dimenzije slike,
* broj kanala,
* color space,
* kompresiju,
* metadata segment,
* pixel segment,
* CRC32 kontrolnu sumu.

---

## 10. Struktura MSI fajla

Struktura fajla:

| Offset | Size | Polje       | Opis                     |
| -----: | ---: | ----------- | ------------------------ |
|      0 |    4 | MAGIC       | `MSI0`                   |
|      4 |    2 | VERSION     | Verzija formata          |
|      6 |    2 | HEADER_LEN  | Dužina header-a          |
|      8 |    4 | WIDTH       | Širina slike             |
|     12 |    4 | HEIGHT      | Visina slike             |
|     16 |    1 | CHANNELS    | Broj kanala              |
|     17 |    1 | COLORSPACE  | Prostor boja             |
|     18 |    1 | COMPRESSION | Tip kompresije           |
|     19 |    1 | RESERVED    | Poravnanje               |
|     20 |    4 | META_LEN    | Dužina metadata segmenta |
|     24 |    4 | PIXEL_LEN   | Dužina pixel segmenta    |
|     28 |    N | META        | Metadata                 |
| 28 + N |    M | PIXELS      | Pixel podaci             |
|   kraj |    4 | CRC32       | Kontrolna suma           |

---

## 11. MSI color space

Za neparan broj indeksa implementiran je:

```text
COLORSPACE = 3 = HSV
```

Prilikom export-a u MSI, RGB pikseli se konvertuju u HSV.

Prilikom import-a iz MSI, HSV pikseli se vraćaju u RGB i čuvaju kao PNG.

---

## 12. MSI kompresija

Implementirane su dve kompresije za neparan broj indeksa:

```text
2 = Huffman
4 = Downsampling 4:2:0 MPEG-2
```

### Huffman kompresija

Huffman je lossless kompresija.
Pixel podaci se kompresuju na osnovu učestalosti bajtova.

Metadata segment čuva:

* dužinu originalnog pixel segmenta,
* dužinu kompresovanog segmenta,
* broj bitova,
* Huffman tabelu kodova.

### Downsampling 4:2:0 MPEG-2

MPEG-2 downsampling je lossy kompresija.
Čuva se puna rezolucija jednog kanala, dok se druga dva kanala smanjuju po principu 4:2:0.

Ovaj pristup smanjuje količinu pixel podataka, ali može dovesti do manjeg gubitka kvaliteta.

---

## 13. CRC32

MSI fajl na kraju sadrži CRC32 kontrolnu sumu.

CRC32 se računa nad svim bajtovima fajla pre same CRC vrednosti.

Prilikom učitavanja MSI fajla server:

1. pročita CRC32 iz fajla,
2. ponovo izračuna CRC32,
3. uporedi vrednosti,
4. odbacuje fajl ako je korumpiran.

Ako CRC provera ne uspe, server vraća grešku i aplikacija se ne ruši.

---

## 14. Klijent

Klijent je implementiran kao WPF desktop aplikacija.

Funkcionalnosti klijenta:

* učitavanje standardnih formata,
* učitavanje MSI formata,
* prikaz originalne slike,
* prikaz obrađene slike,
* poređenje originala i rezultata,
* izbor filtera,
* podešavanje parametara filtera,
* slanje zahteva serveru,
* undo/redo poslednje 3 operacije,
* čuvanje rezultata,
* export u MSI,
* izbor MSI kompresije,
* zoom,
* pan,
* klijentsko logovanje.

---

## 15. Podržani formati u klijentu

Klijent podržava učitavanje:

```text
PNG
JPG
JPEG
BMP
GIF
MSI
```

Standardni formati se učitavaju direktno.

MSI fajl se šalje serveru preko endpoint-a:

```http
POST /api/Image/import-msi
```

Server ga dekodira i vraća PNG prikaz.

---

## 16. Preview i compare

Klijent prikazuje dve slike:

* leva strana: originalna slika,
* desna strana: rezultat / preview.

Ovo omogućava poređenje pre i posle primene filtera.

---

## 17. Undo / Redo

Klijent podržava undo/redo za poslednje 3 operacije.

Svaki put kada se primeni filter, prethodno stanje slike se čuva u undo stack.

Ako korisnik klikne Undo, trenutno stanje se prebacuje u redo stack, a prethodna slika se vraća.

Ograničenje na 3 operacije je u skladu sa zahtevom projekta.

---

## 18. Zoom i pan

Klijent podržava:

* Zoom +
* Zoom -
* Reset Zoom

Pan je omogućen preko `ScrollViewer` kontrole. Kada se slika uveća, korisnik može da je pomera horizontalnim i vertikalnim scroll bar-ovima.

---

## 19. Logovanje

Logovanje postoji na obe strane.

### Server logovanje

Server loguje:

* naziv filtera,
* ime fajla,
* veličinu fajla,
* izlaznu veličinu,
* greške,
* batch operacije,
* MSI import/export.

### Klijentsko logovanje

Klijent loguje u fajl:

```text
logs/client-log.txt
```

Log sadrži:

* vreme,
* korisnika,
* akciju,
* trajanje,
* detalje,
* greške.

Primer:

```text
[2026-06-12 16:45:18] [INFO] User=Petar Action=ApplyFilter ElapsedMs=84 Details=Filter=invert
[2026-06-12 16:45:24] [INFO] User=Petar Action=ExportMSI ElapsedMs=157 Details=Compression=2
```

---

## 20. Testovi

Projekat sadrži automatizovane xUnit server testove.

Test projekat:

```text
MMS.Server.Tests
```

Broj server testova:

```text
12
```

Testovi pokrivaju:

* Invert filter,
* Brightness filter,
* Pixelate filter,
* invalidan pixelate block size,
* Emboss Laplacian filter,
* Edge Enhance filter,
* Time Warp filter,
* Jarvis-Judice-Ninke filter,
* Colorize Grayscale filter,
* MSI Huffman encode/decode,
* MSI MPEG-2 encode/decode,
* CRC32 detekciju korumpiranog MSI fajla.

Svi testovi prolaze:

```text
12 Passed
0 Failed
```

---

## 21. Manualni UI testovi

Zbog WPF prirode aplikacije, UI testovi su dokumentovani kao manualni testovi.

| Test         | Koraci                                | Očekivani rezultat                   | Status |
| ------------ | ------------------------------------- | ------------------------------------ | ------ |
| Open Image   | Učitati PNG/JPG/BMP/GIF               | Slika se prikazuje u klijentu        | Prošao |
| Apply Filter | Izabrati filter i kliknuti Apply      | Rezultat se prikazuje desno          | Prošao |
| Undo/Redo    | Primena više filtera, zatim Undo/Redo | Slika se vraća kroz poslednja stanja | Prošao |
| Export MSI   | Izabrati kompresiju i exportovati     | Kreira se `.msi` fajl                | Prošao |
| Open MSI     | Učitati `.msi` fajl                   | Slika se dekodira i prikazuje        | Prošao |

---

## 22. Golden skup

Za evaluaciju je kreiran golden skup slika u folderu:

```text
golden
```

Skup sadrži 9 reprezentativnih slika:

```text
01-shapes.jpg
02-text.png
03-nature.jpeg
04-portrait.jpg
05-city.jpeg
06-texture.jpeg
07-bw.jpg
08-gradient.png
09-colourful.jpg
```

Golden skup pokriva različite tipove vizuelnog sadržaja:

* geometrijske oblike,
* tekst,
* prirodne fotografije,
* portret,
* gradsku scenu,
* teksture,
* crno-belu sliku,
* gradijent,
* šarenu ilustraciju.

---

## 23. Benchmark merenja

Benchmark aplikacija se nalazi u projektu:

```text
MMS.Benchmark
```

Benchmark meri:

* svih 8 filtera,
* batch lanac od 3 filtera,
* MSI Huffman encode,
* MSI Huffman decode,
* MSI MPEG-2 encode,
* MSI MPEG-2 decode.

Svaka operacija se meri:

```text
10 ponavljanja
```

Rezultati se čuvaju u CSV fajlu:

```text
benchmark-results.csv
```

CSV sadrži:

```text
Operation
Image
AverageMs
MinMs
MaxMs
InputSizeBytes
OutputSizeBytes
```

---

## 24. Rezultati benchmark-a

Prosečna vremena izvršavanja po operaciji:

| Operacija           | Prosečno vreme |
| ------------------- | -------------: |
| Pixelate            |       92.21 ms |
| MSI Encode MPEG-2   |      152.97 ms |
| Invert              |      318.80 ms |
| Brightness          |      334.48 ms |
| Time Warp           |      361.50 ms |
| Colorize Grayscale  |      368.14 ms |
| MSI Decode MPEG-2   |      384.49 ms |
| MSI Encode Huffman  |      555.80 ms |
| Batch 3 filtera     |      748.77 ms |
| Emboss Laplacian    |      779.68 ms |
| Edge Enhance        |      794.42 ms |
| Jarvis-Judice-Ninke |      814.07 ms |
| MSI Decode Huffman  |     1242.84 ms |

---

## 25. Analiza performansi

Najbrža operacija je `Pixelate`, jer obrađuje sliku blokovski i ne koristi kompleksne matematičke operacije nad svakim pikselom.

`Invert` i `Brightness` su relativno brzi jer koriste jednostavne aritmetičke operacije nad RGB komponentama.

`Emboss Laplacian` i `Edge Enhance` su sporiji jer koriste konvolucioni kernel 3x3, pa za svaki piksel obrađuju i susedne piksele.

`Jarvis-Judice-Ninke` je jedan od najzahtevnijih filtera jer koristi dithering i difuziju greške kroz susedne piksele.

MSI MPEG-2 encode/decode je brži od Huffman implementacije, jer Huffman zahteva izgradnju stabla, generisanje kodova i bit-level dekodiranje.

Batch obrada tri filtera ima očekivano veće vreme jer se filteri izvršavaju redom.

---

## 26. Pokretanje projekta

### 1. Pokretanje servera

U Visual Studio-u izabrati:

```text
MMS.Server
```

kao startup project i pokrenuti preko HTTPS profila.

Swagger se otvara na adresi:

```text
https://localhost:7238/swagger
```

Server API baza:

```text
https://localhost:7238/api/Image
```

### 2. Pokretanje klijenta

Desni klik na:

```text
MMS.Client
```

zatim:

```text
Debug → Start New Instance
```

Pre pokretanja klijenta, server mora biti aktivan.

### 3. Pokretanje testova

U Visual Studio-u:

```text
Test → Run All Tests
```

Očekivani rezultat:

```text
12 Passed
0 Failed
```

### 4. Pokretanje benchmark-a

Izabrati:

```text
MMS.Benchmark
```

kao startup project i pokrenuti aplikaciju.

Benchmark koristi slike iz foldera:

```text
golden
```

Rezultat se generiše u fajlu:

```text
benchmark-results.csv
```

---

## 27. Zaključak

Projekat implementira kompletan klijent-server sistem za obradu slika.

Server obavlja filtersku obradu, validaciju, MSI serijalizaciju/deserijalizaciju, kompresiju i CRC proveru.

Klijent omogućava korisniku rad sa slikama kroz grafički interfejs, preview, compare, undo/redo, zoom/pan, MSI import/export i izbor kompresije.

Implementiran je sopstveni MSI format sa HSV color space-om, Huffman kompresijom, MPEG-2 4:2:0 downsampling-om i CRC32 proverom integriteta.

Sistem je testiran automatizovanim server testovima, manualnim UI testovima i benchmark merenjima nad golden skupom slika.

Projekat ispunjava funkcionalne i nefunkcionalne zahteve zadatka.
