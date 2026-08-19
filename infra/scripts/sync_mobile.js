const path = require("path");
const fs = require("fs-extra");

const source = path.resolve(__dirname, "../../mobile");
const target = path.resolve(
  process.env.TEMP || process.env.TMPDIR || "/tmp",
  "egitimussu_mobile_workspace"
);

async function run() {
  console.log("Source:", source);
  console.log("Target:", target);

  if (!fs.existsSync(source)) {
    throw new Error("Source mobile workspace not found: " + source);
  }

  await fs.ensureDir(target);

  await fs.copy(source, target, {
    overwrite: true,
    errorOnExist: false,
    filter: (src) => {
      // CocoaPods artefaktları (Pods/, .symlinks/, *.lock) göreli pub-cache
      // yolları içerir; bu yollar YALNIZ üretildikleri konumdan (gerçek
      // mobile/) çözülür. Daha derin temp workspace'e kopyalanınca kırılır
      // (ör. flutter_local_notifications ActionEventSink.h "No such file").
      // Bunları dışla → workspace'te `flutter run` iOS build'i `pod install`i
      // konumuna göre taze çalıştırır ve doğru yolları üretir.
      const segments = path.relative(source, src).split(path.sep);
      if (segments.includes("Pods") || segments.includes(".symlinks")) {
        return false;
      }
      const base = path.basename(src);
      if (base === "Podfile.lock" || base === "Manifest.lock") {
        return false;
      }
      return !src.includes("build") &&
             !src.includes(".dart_tool") &&
             !src.includes(".idea") &&
             !src.match(/tmp_.*\.log/) &&
             !src.endsWith(".iml");
    }
  });

  console.log(target);
}

run().catch(err => {
  console.error(err);
  process.exit(1);
});