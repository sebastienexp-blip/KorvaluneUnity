#!/bin/bash
# Test de l'APK dans l'émulateur : installation, lancement, captures, processus vivant, journal sans plantage.
APK="$1"; PKG=com.korvalune.game; OUT=ci-out/android
mkdir -p $OUT; R=$OUT/test.md; ok=1
echo "# Test de l'APK dans un émulateur Android (API 30, x86_64)" > $R; echo >> $R
adb wait-for-device; adb shell getprop ro.build.version.release | sed 's/^/- Android /' >> $R
ls -la "$APK" | awk '{printf "- APK : %.1f Mo\n", $5/1048576}' >> $R
if adb install -r -g "$APK" > $OUT/install.txt 2>&1; then echo "- ✅ Installation" >> $R; else echo "- ❌ Installation : $(tail -1 $OUT/install.txt)" >> $R; ok=0; fi
adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1
for t in 20 45 90; do
  sleep $(( t - ${prev:-0} )); prev=$t
  adb exec-out screencap -p > $OUT/ecran_${t}s.png
  if adb shell pidof $PKG > /dev/null; then echo "- ✅ Jeu en cours d'exécution à ${t} s (capture ecran_${t}s.png)" >> $R; else echo "- ❌ Le jeu ne tourne plus à ${t} s" >> $R; ok=0; break; fi
done
# toucher l'écran (bouton « Nouveau héros » / créer) puis capture
adb shell input tap 1200 900; sleep 8; adb exec-out screencap -p > $OUT/ecran_toucher.png
adb shell pidof $PKG > /dev/null && echo "- ✅ Toujours vivant après une interaction tactile" >> $R || { echo "- ❌ Arrêt après interaction" >> $R; ok=0; }
# ---- partie automatique : lanceur → sélection → vallée (héroïne créée comme par un joueur), marche, panneaux
echo >> $R; echo "## Partie automatique dans la vallée" >> $R
adb shell am force-stop $PKG; sleep 2
adb shell "mkdir -p /sdcard/Android/data/$PKG/files && echo 1 > /sdcard/Android/data/$PKG/files/autotest.txt"
adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1
seen=""; start=$(date +%s)
while [ $(( $(date +%s) - start )) -lt 420 ]; do
  sleep 4
  adb logcat -d -s Unity:V > $OUT/partie_brut.txt 2>/dev/null
  for m in "arrivée:1_arrivee" "marche 10 s:2_marche" "panneau : arbre:3_arbre" "panneau : inventaire:4_inventaire" "panneau : menu:5_menu" "panneau : options:6_options" "TERMINÉ:7_fin"; do
    key="${m%%:*}"; name="${m##*:}"
    case "$seen" in *"|$name|"*) continue;; esac
    if grep -q "\[Korvalune\]\[test\] .*$key" $OUT/partie_brut.txt; then sleep 3; adb exec-out screencap -p > $OUT/jeu_${name}.png; seen="$seen|$name|"; fi
  done
  case "$seen" in *"|7_fin|"*) break;; esac
  adb shell pidof $PKG > /dev/null || { echo "- ❌ Le jeu s'est arrêté pendant la partie" >> $R; ok=0; break; }
done
grep -E "\[Korvalune\]" $OUT/partie_brut.txt | sed -E 's/^.*\[Korvalune\]/[Korvalune]/' > $OUT/partie.txt; rm -f $OUT/partie_brut.txt
sed 's/^/    /' $OUT/partie.txt | head -40 >> $R
if grep -q "CHUTE\|chute rattrapée\|collision du terrain absente" $OUT/partie.txt; then echo "- ⚠️ Chute détectée (voir ci-dessus)" >> $R; fi
if grep -q "TERMINÉ" $OUT/partie.txt; then echo "- ✅ Partie automatique terminée (captures jeu_*.png)" >> $R; else echo "- ⚠️ Partie automatique inachevée (voir partie.txt)" >> $R; fi
grep -q " OK" $OUT/partie.txt && ! grep -q "→ CHUTE" $OUT/partie.txt && echo "- ✅ Le héros reste au sol dans la vallée" >> $R

adb logcat -d > $OUT/logcat_complet.txt
grep -E "Unity|$PKG|FATAL|AndroidRuntime|libc|DEBUG" $OUT/logcat_complet.txt | tail -400 > $OUT/logcat_unity.txt
rm -f $OUT/logcat_complet.txt
if grep -qE "FATAL EXCEPTION|Fatal signal|SIGSEGV|SIGABRT|il2cpp.*crash" $OUT/logcat_unity.txt; then echo "- ❌ Plantage détecté dans le journal" >> $R; ok=0; grep -E "FATAL|Fatal signal|SIG" $OUT/logcat_unity.txt | head -10 | sed 's/^/    /' >> $R; else echo "- ✅ Aucun plantage dans le journal" >> $R; fi
n=$(grep -cE " E Unity" $OUT/logcat_unity.txt); echo "- Erreurs Unity dans le journal : $n" >> $R
grep -E " E Unity" $OUT/logcat_unity.txt | head -15 | sed 's/^/    /' >> $R
echo >> $R; [ $ok = 1 ] && echo "**RÉSULTAT : OK**" >> $R || echo "**RÉSULTAT : ÉCHEC**" >> $R
cat $R
exit 0
