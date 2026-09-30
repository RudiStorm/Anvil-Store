(function () {
    const root = document.documentElement;
    const button = document.getElementById('theme-toggle');
    const saved = window.localStorage.getItem('anvil-store-theme');
    if (saved === 'light' || saved === 'dark') root.dataset.theme = saved;
    function update() {
        const light = root.dataset.theme === 'light';
        if (button) button.textContent = light ? 'Dark mode' : 'Light mode';
    }
    update();
    if (button) button.addEventListener('click', function () {
        root.dataset.theme = root.dataset.theme === 'light' ? 'dark' : 'light';
        window.localStorage.setItem('anvil-store-theme', root.dataset.theme);
        update();
    });
    document.addEventListener('click', function (event) {
        const copyButton = event.target.closest('[data-copy-command]');
        if (!copyButton) return;
        const command = copyButton.dataset.copyCommand;
        if (!command) return;
        const status = copyButton.parentElement.nextElementSibling;
        navigator.clipboard.writeText(command).then(function () {
            copyButton.textContent = 'Copied';
            if (status) status.textContent = 'Command copied to clipboard.';
            window.setTimeout(function () { copyButton.textContent = 'Copy'; }, 1800);
        });
    });
}());
