export async function refreshLogin() {
    await fetch("/Logout/Refresh");
}