var shell = new ActiveXObject("WScript.Shell");
var desktop = shell.SpecialFolders("Desktop");
var fso = new ActiveXObject("Scripting.FileSystemObject");
var lnk = desktop + "\\KanBan.lnk";
if (fso.FileExists(lnk)) {
  fso.DeleteFile(lnk);
}
