using System.Collections;
using System.Collections.Generic;
using Slafurry.Core.Abstract;
using Slafurry.Utils.Pooling;
using UnityEngine;

/// <summary>
/// Pemilik semua pool peluru. Satu-satunya tempat Bullet dibuat, dan satu-
/// satunya tempat Bullet dikembalikan.
///
/// Tiap prefab peluru punya poolnya sendiri, jadi peluru player dan peluru
/// enemy bisa hidup berdampingan. Pool dibuat otomatis saat prefab itu pertama
/// dipakai - tidak perlu didaftarkan di inspector.
///
/// LocalSingleton, bukan GameSystem: peluru tidak perlu bertahan antar scene.
/// Kalau manager ini ikut DontDestroyOnLoad, poolnya akan membawa GameObject
/// dari scene sebelumnya, dan scene berikutnya bisa memakai peluru yang
/// dikembalikan ke pool bersama-sama isi scene lama.
///
/// Pool dibangun di OnSingletonAwake, bukan di Initialize(). Semua GameSystem
/// di project ini di-initialize DUA KALI per boot (LoadingSystem.LoadSequence
/// dan BootstrapLoader jalan bersamaan), jadi setup di Initialize() berisiko
/// jalan dua kali.
/// </summary>
public class BulletManager : LocalSingleton<BulletManager>
{
    [Header("Default")]
    [Tooltip("Prefab peluru generik. Dipakai kalau Spawn dipanggil tanpa prefab. Kosong = semua pemanggil wajib menyebut prefabnya sendiri.")]
    [SerializeField] private Bullet defaultPrefab;

    [Header("Pool")]
    [Tooltip("Jumlah peluru per prefab yang dibuat lebih awal, supaya Instantiate tidak terjadi di tengah gameplay.")]
    [SerializeField, Min(0)] private int prewarmCount = 8;
    [Tooltip("Batas peluru per prefab yang boleh ada sekaligus. Setelah penuh, peluru yang dikembalikan di-destroy, bukan di-park.")]
    [SerializeField, Min(1)] private int maxPoolSize = 128;
    [Tooltip("Parent untuk peluru yang di-pool. Kosong = pakai transform milik manager ini.")]
    [SerializeField] private Transform poolRoot;

    // Prefab -> pool-nya. Satu pool per tipe peluru.
    private readonly Dictionary<Bullet, GenericPool<Bullet>> _pools = new Dictionary<Bullet, GenericPool<Bullet>>();

    // Peluru -> pool-nya. Dipakai Release, supaya tidak perlu cari-cari pool
    // mana yang punya instance ini.
    private readonly Dictionary<Bullet, GenericPool<Bullet>> _poolOf = new Dictionary<Bullet, GenericPool<Bullet>>();

    public int PoolCount => _pools.Count;

    public int ActiveCount
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<Bullet, GenericPool<Bullet>> pair in _pools) total += pair.Value.CountActive;
            return total;
        }
    }

    public int InactiveCount
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<Bullet, GenericPool<Bullet>> pair in _pools) total += pair.Value.CountInactive;
            return total;
        }
    }

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        if (defaultPrefab != null) GetOrCreatePool(defaultPrefab);
    }

    protected override void OnSingletonDestroyed()
    {
        foreach (KeyValuePair<Bullet, GenericPool<Bullet>> pair in _pools) pair.Value.Clear();
        _pools.Clear();
        _poolOf.Clear();
    }

    /// <summary>
    /// Ambil peluru dari pool prefab ini, taruh di muzzle, lalu tembakkan.
    /// Prefab null = pakai defaultPrefab.
    /// </summary>
    public Bullet Spawn(Bullet prefab, Vector2 origin, Vector2 direction)
    {
        GenericPool<Bullet> pool = GetOrCreatePool(prefab);
        if (pool == null) return null;

        Bullet bullet = pool.Get();
        _poolOf[bullet] = pool;
        bullet.Launch(origin, direction);
        return bullet;
    }

    /// <summary>Overload singkat untuk pemanggil yang tidak punya referensi prefab.</summary>
    public Bullet Spawn(Vector2 origin, Vector2 direction) => Spawn(defaultPrefab, origin, direction);

    /// <summary>Kembalikan peluru ke pool asalnya. Dipanggil Bullet sendiri saat kena, mati, atau sudah terlalu tua.</summary>
    public void Release(Bullet bullet)
    {
        if (bullet == null) return;
        if (!_poolOf.TryGetValue(bullet, out GenericPool<Bullet> pool)) return;

        _poolOf.Remove(bullet);
        pool.Release(bullet);
    }

    private GenericPool<Bullet> GetOrCreatePool(Bullet prefab)
    {
        if (prefab == null) prefab = defaultPrefab;
        if (prefab == null)
        {
            Debug.LogError($"[{nameof(BulletManager)}] Tidak ada prefab peluru. Set '{nameof(defaultPrefab)}' atau sebut prefabnya di Spawn().", this);
            return null;
        }

        if (_pools.TryGetValue(prefab, out GenericPool<Bullet> existing)) return existing;

        GenericPool<Bullet> pool = new GenericPool<Bullet>(prefab, poolRoot, prewarmCount, maxPoolSize);
        _pools.Add(prefab, pool);
        if (prewarmCount > 0) pool.Prewarm(prewarmCount);
        return pool;
    }

    public override IEnumerator Initialize()
    {
        // Pool sudah dibangun di OnSingletonAwake, jadi tidak bergantung pada
        // Initialize() yang dipanggil dua kali saat boot.
        yield return null;
    }

    public override void PostInitialize()
    {
    }
}
