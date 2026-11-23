using UnityEditor;
using UnityEngine;

// import settings for our sounds in Assets/Resources/Audio (GameAudio plays them):
// Music_*.wav is compressed (Vorbis) and loads in the background, so changing scenes never freezes
// Sfx_*.wav is unpacked when it loads, so effects play with no delay
// if you drop in a new file with the same name it gets the same settings
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
