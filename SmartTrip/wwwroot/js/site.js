document.addEventListener("DOMContentLoaded", function () {
    const title = document.getElementById("typing-title");

    if (!title) return;

    const text = "Пътувай умно.";
    let index = 0;

    function typeText() {
        if (index < text.length) {
            title.textContent += text.charAt(index);
            index++;

            setTimeout(typeText, 100);
        }
    }

    typeText();
});

