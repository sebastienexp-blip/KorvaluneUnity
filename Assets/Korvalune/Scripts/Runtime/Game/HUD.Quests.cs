using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Korvalune.Game
{
    /// <summary>Journal de quêtes : trois onglets (Histoire, Secondaires, Tutoriels), liste des quêtes avec leur état
    /// (en cours, terminée, verrouillée), fiche détaillée (récit, région, niveau conseillé, objectifs, récompenses) et
    /// bouton pour suivre une quête (affichée sous le portrait).</summary>
    public partial class HUD
    {
        GameObject journalPanel; RectTransform journalList; Text journalDetail, journalProgress, journalTrackText; RectTransform journalDetailBox;
        Image journalTrack; readonly List<(Image bg, Text label, Story.Kind kind)> journalTabs = new List<(Image, Text, Story.Kind)>();
        Story.Kind journalKind = Story.Kind.Principale; Story.Def journalSel;

        void BuildJournal(RectTransform root)
        {
            var panel = OrnatePanel(root, "Journal", new Vector2(1780, 1000), "Journal de quêtes");
            journalPanel = panel.gameObject;
            RoundButton(panel, "Fermer", "close", new Vector2(1, 1), new Vector2(-30, -26), 84, ToggleJournal, out _, out _);
            journalProgress = Label(panel, "Avancement", "", 26, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(60, -64), new Vector2(900, 40));
            journalProgress.color = UIStyle.Parch;
            // onglets
            var kinds = new[] { (Story.Kind.Principale, "Histoire"), (Story.Kind.Secondaire, "Secondaires"), (Story.Kind.Tutoriel, "Tutoriels") };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (k, label) = kinds[i];
                var bg = TextButton(panel, "Onglet" + label, label, new Vector2(0, 1), new Vector2(50 + i * 250, -110), new Vector2(236, 66), () => { journalKind = k; journalSel = null; RefreshJournal(); }, out var t, 28);
                journalTabs.Add((bg, t, k));
            }
            // liste défilante
            var view = Img(panel, "Liste", UIStyle.Slot(), new Vector2(0, 1), new Vector2(46, -190), new Vector2(760, 760)); view.type = Image.Type.Sliced; view.raycastTarget = true;
            var vfill = Img(view.rectTransform, "Fond", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(736, 736)); vfill.color = ZoneFill; vfill.raycastTarget = false;
            view.gameObject.AddComponent<RectMask2D>();
            journalList = new GameObject("Contenu", typeof(RectTransform)).GetComponent<RectTransform>();
            journalList.SetParent(view.rectTransform, false); journalList.anchorMin = journalList.anchorMax = new Vector2(0, 1); journalList.pivot = new Vector2(0, 1);
            journalList.sizeDelta = new Vector2(740, 740); journalList.anchoredPosition = new Vector2(10, -10);
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = journalList; sr.viewport = view.rectTransform; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40; sr.inertia = true;
            // fiche
            var side = Img(panel, "Fiche", UIStyle.Slot(), new Vector2(1, 1), new Vector2(-46, -110), new Vector2(900, 840)); side.type = Image.Type.Sliced;
            var sfill = Img(side.rectTransform, "Fond", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(876, 816)); sfill.color = ZoneFill; sfill.raycastTarget = false;
            journalDetailBox = Scroll(side.rectTransform, new Vector2(30, -26), new Vector2(840, 680), out journalDetail, 27);
            journalTrack = TextButton(side.rectTransform, "Suivre", "Suivre cette quête", new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(460, 80), TrackSelected, out journalTrackText, 30);
            journalPanel.SetActive(false);
        }

        public void ToggleJournal()
        {
            if (!journalPanel) return;
            if (bagPanel) bagPanel.SetActive(false); if (treePanel) treePanel.SetActive(false);
            journalPanel.SetActive(!journalPanel.activeSelf);
            if (journalPanel.activeSelf)
            {
                Front(journalPanel);
                var tr = QuestLog.Tracked; if (tr != null) { journalKind = tr.kind; journalSel = tr; }
                RefreshJournal(); QuestEvents.Raise("journal");
            }
            Sound.UI(journalPanel.activeSelf ? "sfx_ui_ouvrir" : "sfx_ui_fermer", 0.7f);
        }

        void TrackSelected()
        {
            if (journalSel == null || !QuestLog.Active(journalSel)) return;
            QuestLog.Tracked = journalSel; Notify($"Quête suivie : {journalSel.title}"); RefreshJournal();
        }

        static string StatusOf(Story.Def d) => QuestLog.Done(d.id) ? "done" : QuestLog.Available(d) ? "active" : "locked";

        void RefreshJournal()
        {
            if (!journalPanel) return;
            int chDone = 0, chTot = 0, sDone = 0, sTot = 0, tDone = 0, tTot = 0;
            foreach (var d in Story.All)
            {
                bool dn = QuestLog.Done(d.id);
                if (d.kind == Story.Kind.Principale) { chTot++; if (dn) chDone++; } else if (d.kind == Story.Kind.Secondaire) { sTot++; if (dn) sDone++; } else { tTot++; if (dn) tDone++; }
            }
            journalProgress.text = $"Histoire {chDone}/{chTot}   ·   Secondaires {sDone}/{sTot}   ·   Tutoriels {tDone}/{tTot}";
            foreach (var (bg, label, kind) in journalTabs) { bool on = kind == journalKind; bg.color = on ? Color.white : new Color(0.62f, 0.55f, 0.48f); label.color = on ? UIStyle.Gold : UIStyle.Parch; }

            for (int i = journalList.childCount - 1; i >= 0; i--) { var c = journalList.GetChild(i).gameObject; if (Application.isPlaying) { c.SetActive(false); Destroy(c); } else DestroyImmediate(c); }
            var list = new List<Story.Def>();
            foreach (var d in Story.All) if (d.kind == journalKind) list.Add(d);
            // en cours d'abord, puis terminées, puis verrouillées (l'histoire garde l'ordre des chapitres)
            if (journalKind != Story.Kind.Principale)
                list.Sort((a, b) => { int ra = StatusOf(a) == "active" ? 0 : StatusOf(a) == "done" ? 1 : 2, rb = StatusOf(b) == "active" ? 0 : StatusOf(b) == "done" ? 1 : 2; return ra != rb ? ra.CompareTo(rb) : a.level.CompareTo(b.level); });
            if (journalSel == null || journalSel.kind != journalKind) { journalSel = null; foreach (var d in list) if (StatusOf(d) == "active") { journalSel = d; break; } if (journalSel == null && list.Count > 0) journalSel = list[0]; }
            var tracked = QuestLog.Tracked;
            float y = 0;
            foreach (var d in list)
            {
                var dd = d; string st = StatusOf(d);
                string mark = st == "done" ? "<color=#8fd16a>√</color>" : st == "active" ? "<color=#ffd24a>●</color>" : "<color=#9a8f80>○</color>";
                string head = d.kind == Story.Kind.Principale ? $"Chapitre {Story.ChapterNumber(d)} — " : "";
                string sub2 = st == "locked" ? "Verrouillée" : st == "done" ? "Terminée" : QuestLog.Objective(d);
                if (d == tracked) sub2 = "<color=#ffd24a>Suivie</color> · " + sub2;
                var bg = TextButton(journalList, "Quête_" + d.id, "", new Vector2(0, 1), new Vector2(0, -y), new Vector2(716, 104), () => { journalSel = dd; RefreshJournal(); }, out var t, 26);
                bg.rectTransform.pivot = new Vector2(0, 1); bg.rectTransform.anchoredPosition = new Vector2(0, -y);
                if (d == journalSel) bg.color = new Color(1f, 0.88f, 0.6f);
                else if (st == "locked") bg.color = new Color(0.6f, 0.56f, 0.52f);
                t.supportRichText = true; t.alignment = TextAnchor.MiddleLeft; t.rectTransform.sizeDelta = new Vector2(680, 96);
                t.text = $"{mark}  <b>{head}{d.title}</b>\n<size=21>{sub2}</size>";
                t.color = st == "locked" ? new Color(0.8f, 0.75f, 0.68f) : UIStyle.Parch;
                y += 112;
            }
            journalList.sizeDelta = new Vector2(740, Mathf.Max(740, y + 10));
            FillJournalDetail();
        }

        void FillJournalDetail()
        {
            var d = journalSel;
            if (d == null) { journalDetail.text = "Aucune quête."; journalTrack.gameObject.SetActive(false); return; }
            string st = StatusOf(d);
            var sb = new System.Text.StringBuilder();
            string kind = d.kind == Story.Kind.Principale ? $"Histoire principale — chapitre {Story.ChapterNumber(d)}" : d.kind == Story.Kind.Secondaire ? "Quête secondaire" : "Tutoriel";
            sb.Append($"<size=36><color=#f0c070><b>{d.title}</b></color></size>\n<color=#d8c8a8>{kind}</color>\n");
            if (d.scene != null) sb.Append($"Région : {QuestLog.RegionName(d.scene)}\n");
            if (d.kind != Story.Kind.Tutoriel) sb.Append($"Niveau conseillé : {d.level}\n");
            sb.Append("\n").Append(d.desc).Append("\n\n");
            sb.Append("<color=#f0c070><b>Objectifs</b></color>\n");
            if (d.kind == Story.Kind.Principale)
            {
                var q = Quest.ForScene(d.scene);
                if (q != null)
                    for (int i = 0; i < q.steps.Count; i++)
                    {
                        var o = q.steps[i]; bool ok = i < q.current; bool cur = i == q.current;
                        sb.Append(ok ? "<color=#8fd16a>√</color> " : cur ? "<color=#ffd24a>●</color> " : "<color=#9a8f80>·</color> ")
                          .Append(cur ? $"<b>{o.text}</b>" : o.text).Append(cur && !string.IsNullOrEmpty(o.enemyId) ? $" ({o.done}/{o.count})" : "").Append("\n");
                    }
                else sb.Append(st == "done" ? "<color=#8fd16a>√</color> Chapitre terminé\n" : $"● Se rendre dans la région : {QuestLog.RegionName(d.scene)} (pierre de voyage)\n● Accomplir les objectifs du chapitre\n");
            }
            else
            {
                var s = QuestLog.State(d.id);
                for (int i = 0; i < d.objs.Count; i++)
                {
                    var o = d.objs[i]; bool ok = st == "done" || (s != null && i < s.step); bool cur = st == "active" && s != null && i == s.step;
                    sb.Append(ok ? "<color=#8fd16a>√</color> " : cur ? "<color=#ffd24a>●</color> " : "<color=#9a8f80>·</color> ").Append(cur ? $"<b>{o.text}</b>" : o.text)
                      .Append(o.count > 1 ? (cur ? $" ({s.done}/{o.count})" : $" ({(ok ? o.count : 0)}/{o.count})") : "").Append(o.scene != null ? $" — {QuestLog.RegionName(o.scene)}" : "").Append("\n");
                }
            }
            int lvl = player ? player.level : 1;
            float xp = Mathf.Round(PlayerController.XpForLevel(lvl) * d.xpShare);
            long gold = d.gold + (long)(lvl * 8 * (d.kind == Story.Kind.Principale ? 4 : 1));
            sb.Append($"\n<color=#f0c070><b>Récompenses</b></color>\n{xp:N0} points d'expérience (au niveau {lvl})\n{gold:N0} pièces d'or\n");
            if (d.kind == Story.Kind.Principale) sb.Append("Chaque objectif du chapitre rapporte aussi de l'expérience.\n");
            if (st == "locked") { var r = Story.Get(d.requires); sb.Append($"\n<color=#c8a080>Se débloque après : {r?.title}</color>\n"); }
            journalDetail.text = sb.ToString(); journalDetail.supportRichText = true;
            FitScroll(journalDetailBox, journalDetail, 680);
            journalDetailBox.anchoredPosition = Vector2.zero;
            bool can = st == "active";
            journalTrack.gameObject.SetActive(can);
            journalTrackText.text = QuestLog.Tracked == d ? "Quête suivie" : "Suivre cette quête";
        }
    }
}
