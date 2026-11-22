using UnityEditor;
using UnityEngine;

// Import settings for the game's sounds in Assets/Resources/Audio (played by GameAudio):
//  Music_*.wav  compressed in memory (Vorbis), loaded in the background, so a scene change
//               never stalls on decoding a whole loop.
//  Sfx_*.wav    decompressed on load, so effects start with no delay.
// Any replacement file dropped in under the same name gets the same settings.
public class GameAudioImporter : AssetPostprocessor
{
    private const string AudioFolder = "Assets/Resources/Audio/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioFolder, System.StringComparison.OrdinalIgnoreCase))
            return;

        AudioImporter importer = (AudioImporter)assetImporter;
        bool music = System.IO.Path.GetFileName(assetPath).StartsWith("Music_", System.StringComparison.OrdinalIgnoreCase);

        importer.forceToMono = true;
        importer.loadInBackground = music;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = music ? 0.55f : 0.7f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}
