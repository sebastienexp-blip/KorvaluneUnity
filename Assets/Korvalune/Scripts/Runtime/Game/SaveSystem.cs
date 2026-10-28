using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Korvalune.Game
{
    [Serializable] public class MorphSave { public string id; public float v; }

    [Serializable] public class AppearanceSave
    {
        public int skin = -1, eyes = -1, hair = -1, hairVariant, brows = -1, lashes = -1;
        public Color hairTint = Color.white;
        public List<MorphSave> morphs = new List<MorphSave>();
    }

    [Serializable] public class CharacterSave
    {
        public string name = "";
        public string classId = "KaelDraven";
        public bool female;
        public int level = 1; public float xp; public int skillPoints = 1; public long gold;
        public int difficulty;          // 0 Normal … 6 Tourment IV
        public List<int> skillRanks = new List<int>();
        public List<int> loadout = new List<int>();
        public List<ItemSave> inventory = new List<ItemSave>();
        public List<ItemSave> equipped = new List<ItemSave>();
        public Wallet wallet = new Wallet();
        public AppearanceSave look = new AppearanceSave();
        public string created, lastPlayed;
        public float playSeconds;
    }

    [Serializable] public class AccountSave
    {
        public const int Slots = 5;
        public string accountId = "local";
        public List<CharacterSave> characters = new List<CharacterSave>();
        public int lastSelected;
        public List<ItemSave> stash = new List<ItemSave>();          // coffre de banque partagé entre les personnages
    }

    /// <summary>Sauvegarde locale par compte (JSON dans le dossier persistant de l'appareil) : jusqu'à 5 personnages par
    /// compte. Le format est prêt à être synchronisé avec un serveur plus tard.</summary>
    public static class SaveSystem
    {
        public static AccountSave Account { get; private set; }
        public static CharacterSave Current { get; set; }
        public static string AccountId = "local";
        static string Dir => Path.Combine(Application.persistentDataPath, "comptes");
        static string FilePath => Path.Combine(Dir, AccountId + ".json");

        public static AccountSave Load()
        {
            try
            {
                if (File.Exists(FilePath)) Account = JsonUtility.FromJson<AccountSave>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Debug.LogWarning("[Sauvegarde] lecture impossible : " + e.Message); }
            if (Account == null) Account = new AccountSave { accountId = AccountId };
            return Account;
        }

        public static void Save()
        {
            if (Account == null) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Account, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("[Sauvegarde] écriture impossible : " + e.Message); }
        }

        /// <summary>Nom valide : 2 à 16 caractères (lettres, espaces, apostrophes, traits d'union), unique dans le compte.</summary>
        public static string ValidateName(string name, CharacterSave except = null)
        {
            name = (name ?? "").Trim();
            if (name.Length < 2) return "Le nom doit contenir au moins 2 caractères.";
            if (name.Length > 16) return "Le nom ne peut pas dépasser 16 caractères.";
            foreach (var ch in name) if (!(char.IsLetter(ch) || ch == ' ' || ch == '\'' || ch == '-')) return "Lettres, espaces, apostrophes et traits d'union uniquement.";
            if (Account != null) foreach (var c in Account.characters) if (c != except && string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)) return "Ce nom est déjà utilisé sur ce compte.";
            return null;
        }

        public static CharacterSave Create(string name, string classId, bool female, AppearanceSave look)
        {
            if (Account == null) Load();
            if (Account.characters.Count >= AccountSave.Slots) return null;
            var c = new CharacterSave { name = name.Trim(), classId = classId, female = female, look = look ?? new AppearanceSave(), created = DateTime.UtcNow.ToString("o"), lastPlayed = DateTime.UtcNow.ToString("o") };
            Account.characters.Add(c); Account.lastSelected = Account.characters.Count - 1; Save();
            return c;
        }

        public static void Delete(CharacterSave c) { if (Account == null) return; Account.characters.Remove(c); Account.lastSelected = 0; Save(); }

        /// <summary>Applique une apparence enregistrée à un personnage instancié (peau, yeux, cheveux, sourcils, cils, morphologie).</summary>
        public static void ApplyLook(GameObject hero, CharacterCatalog cat, AppearanceSave a, bool female)
        {
            if (!hero || cat == null || a == null) return;
            var look = hero.GetComponent<CharacterAppearance>(); if (!look) look = hero.AddComponent<CharacterAppearance>();
            look.female = female;
            if (a.skin >= 0 && a.skin < cat.skins.Count) look.SetSkin(cat.skins[a.skin]);
            if (a.eyes >= 0 && a.eyes < cat.eyes.Count) look.SetEyes(cat.eyes[a.eyes]);
            if (a.hair >= 0 && a.hair < cat.hair.Count) look.Attach("Cheveux", cat.hair[a.hair], a.hairVariant, a.hairTint);
            if (a.brows >= 0 && a.brows < cat.eyebrows.Count) look.Attach("Sourcils", cat.eyebrows[a.brows], 0, Color.white);
            if (a.lashes >= 0 && a.lashes < cat.eyelashes.Count) look.Attach("Cils", cat.eyelashes[a.lashes], 0, Color.white);
            foreach (var m in a.morphs) look.SetMorph(m.id, m.v);
        }
    }
}
