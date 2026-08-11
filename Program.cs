using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace EcoWarga;

// enum untuk mengunci pilihan agar pengguna tidak salah ketik saat input data
public enum JenisSampah
{
    Plastik = 1,
    Kertas = 2,
    Logam = 3,
    Kaca = 4,
    Organik = 5
}

// enum status perjalanan pesanan atau layanan
public enum StatusLayanan
{
    Diajukan,
    Diproses,
    Selesai,
    Dibatalkan
}

// eror jika pengguna memasukkan berat 0 atau minus
public class BeratTidakValidException(string message) : Exception(message);
// eror jika minta jemput ke rumah tapi berat sampahnya kurang dari syarat minimal (2 kg)
public class MinimumPenjemputanException(string message) : Exception(message);

// kelas nasabah buat nampung identitas warga yang daftar bank sampah
public class Nasabah
{
    private string _idNasabah = string.Empty;
    private string _nama = string.Empty;
    private string _alamat = string.Empty;

    // kalo ada teks kosong atau cuma spasi, langsung lempar eror
    public string IdNasabah
    {
        get => _idNasabah;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ID Nasabah tidak boleh kosong.");
            _idNasabah = value.Trim(); // hapus spasi gak berguna di awal dan akhir
        }
    }

    public string Nama
    {
        get => _nama;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Nama Nasabah tidak boleh kosong.");
            _nama = value.Trim();
        }
    }

    public string Alamat
    {
        get => _alamat;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Alamat tidak boleh kosong.");
            _alamat = value.Trim();
        }
    }

    // pembuat objek buat ngisi data awal nasabah
    public Nasabah(string idNasabah, string nama, string alamat)
    {
        IdNasabah = idNasabah;
        Nama = nama;
        Alamat = alamat;
    }

    // ubah cetakan teks biasa biar pas ditampilkan langsung muncul format [ID] Nama - Alamat
    public override string ToString() => $"[{IdNasabah}] {Nama} - {Alamat}";
}

// kelas induk abstrak untuk semua jenis transaksi sampah (gak bisa dibuat objeknya secara langsung)
public abstract class LayananSampah(string idTransaksi, Nasabah nasabah, JenisSampah jenis, double berat, DateTime tanggal, StatusLayanan status = StatusLayanan.Diajukan)
{
    public string IdTransaksi { get; set; } = idTransaksi;
    public Nasabah Nasabah { get; set; } = nasabah;
    public JenisSampah Jenis { get; set; } = jenis;
    public double Berat { get; set; } = berat;
    public DateTime Tanggal { get; set; } = tanggal;
    public StatusLayanan Status { get; set; } = status;

    // fungsi untuk penentu harga per kilo berdasarkan jenis sampah
    public double DapatkanHargaDasarPerKg() => Jenis switch
    {
        JenisSampah.Plastik => 3500,
        JenisSampah.Kertas => 2000,
        JenisSampah.Logam => 8000,
        JenisSampah.Kaca => 1500,
        JenisSampah.Organik => 500,
        _ => 0
    };

    // fungsi wajib yang harus diisi dan dihitung jika beda dengan kelas anak (setoran atau penjemputan)
    public abstract double HitungInsentif();
    public abstract string GetTipeLayanan();

    // fungsi untuk menghitung total poin berdasarkan uang insentif
    public int HitungPoin()
    {
        double insentif = HitungInsentif();
        return (int)Math.Floor(insentif / 1000.0) * 10;
    }

    // cetak rincian transaksi lengkap ke layar
    public virtual void TampilkanRingkasan()
    {
        var culture = new CultureInfo("id-ID"); // format uang rupiah indonesia
        Console.WriteLine($"ID Transaksi : {IdTransaksi}");
        Console.WriteLine($"Tipe Layanan : {GetTipeLayanan()}");
        Console.WriteLine($"Nasabah      : {Nasabah.Nama} ({Nasabah.IdNasabah})");
        Console.WriteLine($"Alamat       : {Nasabah.Alamat}");
        Console.WriteLine($"Jenis Sampah : {Jenis}");
        Console.WriteLine($"Berat        : {Berat} kg");
        Console.WriteLine($"Insentif     : {HitungInsentif().ToString("C", culture)}");
        Console.WriteLine($"Poin         : {HitungPoin()}");
        Console.WriteLine($"Tanggal      : {Tanggal:dd-MM-yyyy HH:mm:ss}");
        Console.WriteLine($"Status       : {Status}");
        Console.WriteLine(new string('-', 50));
    }
}

// kelas anak 1: jika warga datang sendiri untuk membawa sampah ke lokasi maka uang insentif akan utuh
public class SetoranLangsung(string idTransaksi, Nasabah nasabah, JenisSampah jenis, double berat, DateTime tanggal, StatusLayanan status = StatusLayanan.Diajukan)
    : LayananSampah(idTransaksi, nasabah, jenis, berat, tanggal, status)
{
    // rumus insentif utuh = berat * harga per kilo
    public override double HitungInsentif() => Berat * DapatkanHargaDasarPerKg();
    public override string GetTipeLayanan() => "Setoran Langsung";
}

// kelas anak 2: jika petugas menjemput ke rumah warga maka ada potongan ongkos & syarat minimal berat
public class PenjemputanRumah : LayananSampah
{
    public const double BiayaLayanan = 5000; // dipotong 5rb
    public const double MinimumBerat = 2.0;  // minimal 2 kg

    public PenjemputanRumah(string idTransaksi, Nasabah nasabah, JenisSampah jenis, double berat, DateTime tanggal, StatusLayanan status = StatusLayanan.Diajukan)
        : base(idTransaksi, nasabah, jenis, berat, tanggal, status)
    {
        // pengecekan awal: jika sampah di bawah 2 kg langsung stop dan akan mencetak eror
        if (berat < MinimumBerat)
        {
            throw new MinimumPenjemputanException($"Penjemputan rumah membutuhkan berat minimum {MinimumBerat} kg. Input: {berat} kg.");
        }
    }

    // rumus insentif = (berat * harga) dikurangi 5rb ongkos jemput. kalo minus jadinya 0
    public override double HitungInsentif()
    {
        double kotor = Berat * DapatkanHargaDasarPerKg();
        double bersih = kotor - BiayaLayanan;
        return Math.Max(0, bersih);
    }

    public override string GetTipeLayanan() => "Penjemputan Rumah";
}

// antarmuka validasi data untuk mencegah ID nasabah atau ID transaksi ganda
public interface IValidasiData
{
    bool ValidasiNasabahUnik(string idNasabah);
    bool ValidasiTransaksiUnik(string idTransaksi);
    double ValidasiInputBerat(string inputString);
}

// antarmuka penyimpanan file untuk disimpan ke log_transaksi.txt
public interface IPersistensiData
{
    void SimpanKeFile(string path);
    void BacaDariFile(string path);
}

// antarmuka laporan untuk menampilkan ringkasan total transaksi, berat, uang, dan poin
public interface ILaporan
{
    void TampilkanLaporanRingkas();
}

// kelas pengelola utama untuk mengatur data di dalam memori dan penyimpanan file
public class BankSampahManager : IValidasiData, IPersistensiData, ILaporan
{
    // daftar untuk menampung data sementara selama aplikasi berjalan
    private readonly List<Nasabah> _daftarNasabah = [];
    private readonly List<LayananSampah> _daftarLayanan = [];
    private readonly string _logAppPath = "log_aplikasi.txt";

    public List<Nasabah> DaftarNasabah => _daftarNasabah;
    public List<LayananSampah> DaftarLayanan => _daftarLayanan;

    // menulis catatan aktivitas atau eror aplikasi ke dalam file log_aplikasi.txt
    public void Log(string pesan, string level = "INFO")
    {
        try
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {pesan}";
            File.AppendAllText(_logAppPath, logEntry + Environment.NewLine);
        }
        catch { } // dibiarkan kosong agar saat gagal nulis catatan, aplikasinya gak ikut mati
    }

    // mengecek ID nasabah sudah dipakai orang lain atau belum
    public bool ValidasiNasabahUnik(string idNasabah)
    {
        return !_daftarNasabah.Any(n => n.IdNasabah.Equals(idNasabah, StringComparison.OrdinalIgnoreCase));
    }

    // mengecek ID transaksi biar tidak ada yang kembar
    public bool ValidasiTransaksiUnik(string idTransaksi)
    {
        return !_daftarLayanan.Any(l => l.IdTransaksi.Equals(idTransaksi, StringComparison.OrdinalIgnoreCase));
    }

    // ubah masukan teks dari papan ketik jadi angka desimal dan pastikan nilainya lebih dari 0
    public double ValidasiInputBerat(string inputString)
    {
        // menggunakan format titik atau koma agar aman di komputer manapun
        if (!double.TryParse(inputString, NumberStyles.Any, CultureInfo.InvariantCulture, out double berat) &&
            !double.TryParse(inputString, NumberStyles.Any, new CultureInfo("id-ID"), out berat))
        {
            throw new FormatException("Format berat harus berupa angka!");
        }

        if (berat <= 0)
        {
            throw new BeratTidakValidException($"Berat harus lebih besar dari 0 kg. Nilai input: {berat}");
        }

        return berat;
    }

    // simpan semua transaksi dari memori ke file log_transaksi.txt biar permanen
    public void SimpanKeFile(string path)
    {
        try
        {
            using (var writer = new StreamWriter(path, false)) // jika false artinya menimpa isi file lama
            {
                foreach (var item in _daftarLayanan)
                {
                    writer.WriteLine("================================================");
                    writer.WriteLine($"ID_TRANSAKSI:{item.IdTransaksi}");
                    writer.WriteLine($"TANGGAL:{item.Tanggal:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"TIPE:{item.GetTipeLayanan()}");
                    writer.WriteLine($"STATUS:{item.Status}");
                    writer.WriteLine($"ID_NASABAH:{item.Nasabah.IdNasabah}");
                    writer.WriteLine($"NAMA_NASABAH:{item.Nasabah.Nama}");
                    writer.WriteLine($"ALAMAT_NASABAH:{item.Nasabah.Alamat}");
                    writer.WriteLine($"JENIS_SAMPAH:{item.Jenis}");
                    writer.WriteLine($"BERAT:{item.Berat.ToString(CultureInfo.InvariantCulture)}");
                    writer.WriteLine("=================================================");
                    writer.WriteLine();
                }
            }

            string fullPath = Path.GetFullPath(path);
            Log($"Berhasil mencetak {_daftarLayanan.Count} data transaksi ke {fullPath}.");
            Console.WriteLine($"\n[SUKSES] Data transaksi berhasil disimpan");
            Console.WriteLine($"[LOKASI FILE] {fullPath}");
        }
        catch (IOException ex)
        {
            Log($"Gagal menyimpan data transaksi: {ex.Message}", "ERROR");
            Console.WriteLine($"\n[ERROR] Gagal menyimpan file: {ex.Message}");
        }
    }

    // baca ulang data dari log_transaksi.txt saat aplikasi baru dibuka biar data lama tidak hilang
    public void BacaDariFile(string path)
    {
        if (!File.Exists(path)) return; // kalo filenya belum dibuat, lewati saja

        try
        {
            string[] lines = File.ReadAllLines(path);
            string idTrans = "", tipe = "", idNas = "", nama = "", alamat = "", jenisStr = "", statusStr = "";
            double berat = 0;
            DateTime tgl = DateTime.Now;

            // memproses data file baris demi baris
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("ID_TRANSAKSI:")) idTrans = line.Substring("ID_TRANSAKSI:".Length).Trim();
                else if (line.StartsWith("TANGGAL:")) DateTime.TryParse(line.Substring("TANGGAL:".Length).Trim(), out tgl);
                else if (line.StartsWith("TIPE:")) tipe = line.Substring("TIPE:".Length).Trim();
                else if (line.StartsWith("STATUS:")) statusStr = line.Substring("STATUS:".Length).Trim();
                else if (line.StartsWith("ID_NASABAH:")) idNas = line.Substring("ID_NASABAH:".Length).Trim();
                else if (line.StartsWith("NAMA_NASABAH:")) nama = line.Substring("NAMA_NASABAH:".Length).Trim();
                else if (line.StartsWith("ALAMAT_NASABAH:")) alamat = line.Substring("ALAMAT_NASABAH:".Length).Trim();
                else if (line.StartsWith("JENIS_SAMPAH:")) jenisStr = line.Substring("JENIS_SAMPAH:".Length).Trim();
                else if (line.StartsWith("BERAT:")) double.TryParse(line.Substring("BERAT:".Length).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out berat);
                else if (line == "=================================================")
                {
                    // jika bertemu garis pembatas, dan merakit kembali variabel tadi menjadi objek di dalam memori
                    if (!string.IsNullOrEmpty(idTrans))
                    {
                        var nasabah = _daftarNasabah.FirstOrDefault(n => n.IdNasabah.Equals(idNas, StringComparison.OrdinalIgnoreCase));
                        if (nasabah == null)
                        {
                            nasabah = new Nasabah(idNas, nama, alamat);
                            _daftarNasabah.Add(nasabah);
                        }

                        Enum.TryParse(jenisStr, out JenisSampah jenis);
                        Enum.TryParse(statusStr, out StatusLayanan status);

                        // buat objek sesuai jenis pilihannya
                        LayananSampah layanan = tipe.Equals("Penjemputan Rumah", StringComparison.OrdinalIgnoreCase)
                            ? new PenjemputanRumah(idTrans, nasabah, jenis, berat, tgl, status)
                            : new SetoranLangsung(idTrans, nasabah, jenis, berat, tgl, status);

                        if (ValidasiTransaksiUnik(idTrans))
                        {
                            _daftarLayanan.Add(layanan);
                        }
                    }
                }
            }
            Log($"Data berhasil dibaca dari {path}. Total Nasabah: {_daftarNasabah.Count}, Total Transaksi: {_daftarLayanan.Count}");
        }
        catch (Exception ex)
        {
            Log($"Gagal membaca data dari file: {ex.Message}", "ERROR");
        }
    }

    // hitung total transaksi, total berat sampah, total uang, dan total poin warga
    public void TampilkanLaporanRingkas()
    {
        Console.WriteLine("\n==================================================");
        Console.WriteLine("          LAPORAN RINGKAS BANK SAMPAH             ");
        Console.WriteLine("==================================================");

        if (_daftarLayanan.Count == 0)
        {
            Console.WriteLine("Belum ada data transaksi.");
            return;
        }

        int totalTransaksi = _daftarLayanan.Count;
        double totalBerat = _daftarLayanan.Sum(l => l.Berat);
        double totalInsentif = _daftarLayanan.Sum(l => l.HitungInsentif());
        int totalPoin = _daftarLayanan.Sum(l => l.HitungPoin());

        var culture = new CultureInfo("id-ID");
        Console.WriteLine($"Total Transaksi : {totalTransaksi}");
        Console.WriteLine($"Total Berat     : {totalBerat:F2} kg");
        Console.WriteLine($"Total Insentif  : {totalInsentif.ToString("C", culture)}");
        Console.WriteLine($"Total Poin      : {totalPoin} Poin");
        Console.WriteLine("==================================================");
    }

    // untuk menambah data nasabah ke daftar
    public void TambahNasabah(Nasabah nasabah)
    {
        if (!ValidasiNasabahUnik(nasabah.IdNasabah))
        {
            throw new ArgumentException($"Nasabah dengan ID {nasabah.IdNasabah} sudah terdaftar.");
        }
        _daftarNasabah.Add(nasabah);
        Log($"Nasabah baru ditambahkan: {nasabah.IdNasabah} - {nasabah.Nama}");
    }

    // untuk menambah data transaksi ke daftar
    public void TambahLayanan(LayananSampah layanan)
    {
        if (!ValidasiTransaksiUnik(layanan.IdTransaksi))
        {
            throw new ArgumentException($"ID Transaksi {layanan.IdTransaksi} sudah digunakan.");
        }
        _daftarLayanan.Add(layanan);
        Log($"Transaksi baru dibuat: {layanan.IdTransaksi} ({layanan.GetTipeLayanan()}) - Rp {layanan.HitungInsentif()}");
    }
}

// program utama atau tampilan menu
internal class Program
{
    private static readonly BankSampahManager manager = new();
    private const string LogTransaksiPath = "log_transaksi.txt";

    // fungsi utama yang pertama kali dijalankan saat aplikasi mulai
    static void Main(string[] args)
    {
        manager.Log("Aplikasi EcoWarga Dimulai.");
        
        // pas baru nyala, otomatis akan memuat data lama dari file teks
        manager.BacaDariFile(LogTransaksiPath);

        bool exit = false;
        // perulangan menu utama biar aplikasi terus jalan sampai pengguna memilih keluar (9)
        while (!exit)
        {
            TampilkanMenu();
            Console.Write("Pilih Menu (1-9): ");
            string? pilihan = Console.ReadLine();

            switch (pilihan)
            {
                case "1":
                    MenuTambahNasabah();
                    break;
                case "2":
                    MenuTambahTransaksi(isPenjemputan: false); // false = setoran langsung
                    break;
                case "3":
                    MenuTambahTransaksi(isPenjemputan: true);  // true = penjemputan rumah
                    break;
                case "4":
                    MenuTampilkanPolymorphic();
                    break;
                case "5":
                    MenuCariTransaksi();
                    break;
                case "6":
                    MenuUbahStatus();
                    break;
                case "7":
                    manager.SimpanKeFile(LogTransaksiPath);
                    break;
                case "8":
                    manager.TampilkanLaporanRingkas();
                    break;
                case "9":
                    // simpan data otomatis sebelum menutup aplikasi
                    manager.SimpanKeFile(LogTransaksiPath);
                    manager.Log("Aplikasi EcoWarga Ditutup oleh Pengguna.");
                    Console.WriteLine("\nTerima kasih telah menggunakan Sistem Bank Sampah EcoWarga!");
                    exit = true;
                    break;
                default:
                    Console.WriteLine("\n[Peringatan] Pilihan menu tidak valid!");
                    break;
            }

            // jeda sebentar agar teks hasil pilihan menu tidak langsung hilang
            if (!exit)
            {
                Console.WriteLine("\nTekan ENTER untuk melanjutkan...");
                Console.ReadLine();
            }
        }
    }

    // mencetak daftar menu utama ke layar
    private static void TampilkanMenu()
    {
        Console.Clear(); // untuk membersihkan layar terminal
        Console.WriteLine("==================================================");
        Console.WriteLine("    SISTEM BANK SAMPAH DIGITAL \"ECOWARGA\"       ");
        Console.WriteLine("==================================================");
        Console.WriteLine($"[STATUS DATA] Tersimpan: {manager.DaftarNasabah.Count} Nasabah, {manager.DaftarLayanan.Count} Transaksi");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("1. Tambah Data Nasabah");
        Console.WriteLine("2. Catat Setoran Langsung");
        Console.WriteLine("3. Catat Permintaan Penjemputan Rumah");
        Console.WriteLine("4. Tampilkan Seluruh Transaksi");
        Console.WriteLine("5. Cari Transaksi (ID Transaksi / ID Nasabah)");
        Console.WriteLine("6. Ubah Status Layanan");
        Console.WriteLine("7. Simpan Data ke File Log Transaksi");
        Console.WriteLine("8. Tampilkan Laporan Ringkas");
        Console.WriteLine("9. Keluar dari Aplikasi");
        Console.WriteLine("==================================================");
    }

    // formulir untuk mengisi data warga atau nasabah baru
    private static void MenuTambahNasabah()
    {
        Console.WriteLine("\n--- TAMBAH NASABAH BARU ---");
        try
        {
            Console.Write("Masukkan ID Nasabah : ");
            string id = Console.ReadLine() ?? string.Empty;

            Console.Write("Masukkan Nama       : ");
            string nama = Console.ReadLine() ?? string.Empty;

            Console.Write("Masukkan Alamat     : ");
            string alamat = Console.ReadLine() ?? string.Empty;

            var nasabah = new Nasabah(id, nama, alamat);
            manager.TambahNasabah(nasabah);
            Console.WriteLine("\n[SUKSES] Data nasabah berhasil ditambahkan!");
        }
        catch (Exception ex)
        {
            manager.Log($"Gagal Tambah Nasabah: {ex.Message}", "ERROR");
            Console.WriteLine($"\n[ERROR] {ex.Message}");
        }
    }

    // formulir untuk mencatat transaksi sampah
    private static void MenuTambahTransaksi(bool isPenjemputan)
    {
        string judul = isPenjemputan ? "PENJEMPUTAN RUMAH" : "SETORAN LANGSUNG";
        Console.WriteLine($"\n--- TRANSAKSI {judul} ---");

        // wajib punya data nasabah dulu sebelum mencatat transaksi
        if (manager.DaftarNasabah.Count == 0)
        {
            Console.WriteLine("[INFO] Belum ada data nasabah. Silakan tambah nasabah terlebih dahulu (Menu 1).");
            return;
        }

        try
        {
            Console.Write("Masukkan ID Transaksi Unik: ");
            string idTrans = Console.ReadLine() ?? string.Empty;

            Console.Write("Masukkan ID Nasabah       : ");
            string idNasabah = Console.ReadLine() ?? string.Empty;

            // cari data nasabah berdasarkan ID
            Nasabah? nasabah = manager.DaftarNasabah.FirstOrDefault(n => n.IdNasabah.Equals(idNasabah, StringComparison.OrdinalIgnoreCase));
            if (nasabah == null)
            {
                Console.WriteLine("\n[ERROR] Nasabah tidak ditemukan!");
                return;
            }

            //pilihan jenis sampah
            Console.WriteLine("\nPilih Jenis Sampah:");
            Console.WriteLine("1. Plastik (Rp3.500/kg)");
            Console.WriteLine("2. Kertas  (Rp2.000/kg)");
            Console.WriteLine("3. Logam   (Rp8.000/kg)");
            Console.WriteLine("4. Kaca    (Rp1.500/kg)");
            Console.WriteLine("5. Organik (Rp  500/kg)");
            Console.Write("Pilihan (1-5): ");

            if (!int.TryParse(Console.ReadLine(), out int jPilihan) || jPilihan < 1 || jPilihan > 5)
            {
                Console.WriteLine("\n[ERROR] Jenis sampah tidak valid!");
                return;
            }
            JenisSampah jenis = (JenisSampah)jPilihan;

            // perulangan khusus masukan berat: kalo salah ngetik huruf/angka minus, diminta ketik ulang tanpa keluar dari formulir
            double berat;
            while (true)
            {
                try
                {
                    Console.Write("Masukkan Berat (kg)       : ");
                    string inputBerat = Console.ReadLine() ?? string.Empty;
                    berat = manager.ValidasiInputBerat(inputBerat);
                    break; // kalo beratnya benar, keluar dari perulangan ini
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"[FORMAT ERROR] {ex.Message} Silakan input ulang.");
                }
                catch (BeratTidakValidException ex)
                {
                    Console.WriteLine($"[VALIDATION ERROR] {ex.Message} Silakan input ulang.");
                }
            }

            // buat objek transaksi berdasarkan pilihan jenis penjemputan/setoran
            LayananSampah layanan = isPenjemputan
                ? new PenjemputanRumah(idTrans, nasabah, jenis, berat, DateTime.Now)
                : new SetoranLangsung(idTrans, nasabah, jenis, berat, DateTime.Now);

            manager.TambahLayanan(layanan);
            Console.WriteLine("\n[SUKSES] Transaksi berhasil dicatat!");
        }
        catch (MinimumPenjemputanException ex)
        {
            // tangkap eror khusus penjemputan jika kurang dari 2 kg
            manager.Log($"Gagal Tambah Penjemputan: {ex.Message}", "WARN");
            Console.WriteLine($"\n[MINIMUM WEIGHT EXCEPTION] {ex.Message}");
            Console.WriteLine("[INFO] Transaksi dibatalkan secara aman. Program tetap berjalan.");
        }
        catch (Exception ex)
        {
            manager.Log($"Gagal Tambah Transaksi: {ex.Message}", "ERROR");
            Console.WriteLine($"\n[ERROR] {ex.Message}");
        }
    }

    // menampilkan semua data transaksi
    private static void MenuTampilkanPolymorphic()
    {
        Console.WriteLine("\n==================================================");
        Console.WriteLine("       DAFTAR TRANSAKSI BANK SAMPAH ECOWARGA        ");
        Console.WriteLine("====================================================");

        if (manager.DaftarLayanan.Count == 0)
        {
            Console.WriteLine("Belum ada transaksi terdaftar.");
            return;
        }

        // memanggil fungsi cetak ringkasan dari masing-masing jenis transaksi
        foreach (LayananSampah layanan in manager.DaftarLayanan)
        {
            layanan.TampilkanRingkasan();
        }
    }

    // pencarian data transaksi yang sesuai dengan ID transaksi atau ID nasabah
    private static void MenuCariTransaksi()
    {
        Console.WriteLine("\n--- CARI TRANSAKSI ---");
        Console.Write("Masukkan ID Transaksi atau ID Nasabah: ");
        string keyword = Console.ReadLine() ?? string.Empty;

        // menyaring data yang cocok dengan ID Transaksi atau ID Nasabah
        var hasil = manager.DaftarLayanan.Where(l =>
            l.IdTransaksi.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
            l.Nasabah.IdNasabah.Equals(keyword, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (hasil.Count == 0)
        {
            Console.WriteLine("\n[INFO] Data transaksi tidak ditemukan.");
        }
        else
        {
            Console.WriteLine($"\nDitemukan {hasil.Count} transaksi:\n");
            foreach (var item in hasil)
            {
                item.TampilkanRingkasan();
            }
        }
    }

    // mengubah status layanan apakah masih diajukan, diproses, selesai, atau dibatalkan
    private static void MenuUbahStatus()
    {
        Console.WriteLine("\n--- UBAH STATUS LAYANAN ---");
        Console.Write("Masukkan ID Transaksi: ");
        string idTrans = Console.ReadLine() ?? string.Empty;

        LayananSampah? layanan = manager.DaftarLayanan.FirstOrDefault(l => l.IdTransaksi.Equals(idTrans, StringComparison.OrdinalIgnoreCase));
        if (layanan == null)
        {
            Console.WriteLine("\n[ERROR] Transaksi tidak ditemukan.");
            return;
        }

        Console.WriteLine($"Status Saat Ini: {layanan.Status}");
        Console.WriteLine("Pilih Status Baru:");
        Console.WriteLine("1. Diajukan");
        Console.WriteLine("2. Diproses");
        Console.WriteLine("3. Selesai");
        Console.WriteLine("4. Dibatalkan");
        Console.Write("Pilihan (1-4): ");

        if (int.TryParse(Console.ReadLine(), out int pilihan) && pilihan >= 1 && pilihan <= 4)
        {
            StatusLayanan statusBaru = (StatusLayanan)(pilihan - 1); // dikurang 1 karena urutan enum dimulai dari 0
            layanan.Status = statusBaru;
            manager.Log($"Status Transaksi {layanan.IdTransaksi} diubah menjadi {statusBaru}");
            Console.WriteLine($"\n[SUKSES] Status berhasil diubah menjadi {statusBaru}.");
        }
        else
        {
            Console.WriteLine("\n[ERROR] Pilihan status tidak valid!");
        }
    }
}