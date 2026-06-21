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