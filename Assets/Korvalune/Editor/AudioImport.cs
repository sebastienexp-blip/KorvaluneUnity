using UnityEditor;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Réglages d'import audio : musiques lues en continu depuis le disque (peu de mémoire), ambiances compressées
    /// en mémoire, bruitages courts décompressés au chargement (aucune latence).</summary>
    public class AudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var s = ai.defaultSampleSettings;
            if (name.StartsWith("mus_")) { s.loadType = AudioClipLoadType.Streaming; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.5f; ai.loadInBackground = true; }
            else if (name.StartsWith("amb_")) { s.loadType = AudioClipLoadType.CompressedInMemory; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.5f; }
            else { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.6f; }
            ai.defaultSampleSettings = s;
        }
    }
}
