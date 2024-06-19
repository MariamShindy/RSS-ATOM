document.addEventListener("DOMContentLoaded", function () {
    function showSection(sectionId) {
        const sections = ['index-section', 'RegisterBody', 'LoginBody', 'AddFeedBody'];
        sections.forEach(id => {
            document.getElementById(id).style.display = (id === sectionId) ? 'block' : 'none';
        });
    }

    showSection('index-section');

    document.querySelector('.btn-reg').addEventListener('click', function (event) {
        event.preventDefault();
        document.getElementById('Body').style.backgroundColor = 'white';
        showSection('RegisterBody');
    });

    document.querySelector('.btn-log').addEventListener('click', function (event) {
        event.preventDefault();
        document.getElementById('Body').style.backgroundColor = 'black';
        showSection('LoginBody');
    });

    document.querySelector('.btn-register').addEventListener('click', function (event) {
        document.getElementById('Body').style.backgroundColor = 'black';
        showSection('LoginBody');
    });

    document.addEventListener('htmx:afterRequest', function (event) {
        if (event.detail.target.id === 'login-response' && event.detail.xhr.responseText.includes('addFeed-section')) {
            showSection('AddFeedBody');
            document.getElementById('Body').style.backgroundColor = 'white';
            fetchFeeds();
        }
    });

    document.querySelector('#btn-Logout').addEventListener('click', function (event) {
        showSection('index-section');
    });
    document.getElementById('addFeedForm').addEventListener('submit', function (event) {
        event.preventDefault();
        const feedUrl = document.getElementById('addFeed-input').value.trim();

        fetchAndRenderFeedContent(feedUrl);

        document.getElementById('addFeed-input').value = '';
    });
});
function fetchFeeds() {
    fetch('/getFeeds', {
        method: 'POST'
    })
        .then(response => {
            if (response.status === 401) {
                window.location.href = '/login.html';
            } else {
                return response.json();
            }
        })
        .then(feeds => {
            const feedList = document.getElementById('feed-list');
            feedList.innerHTML = '';
            feeds.forEach(feed => {
                const feedItem = document.createElement('li');
                feedItem.className = 'list-group-item d-flex justify-content-between align-items-center';
                feedItem.innerHTML = `${feed.url} <button class='btn btn-danger btn-sm' onclick='removeFeed(${feed.id})'>Remove</button>` + `<button class='btn btn-primary btn-sm' onclick='toggleFeedRender(${feed.Id} ,\"${feed.Url}\")'>Render</button>`;
                feedList.appendChild(feedItem);
            });
        });
}

function toggleFeedRender(feedId, url) {
    let feedContentDiv = document.getElementById(`feed-content-${feedId}`);
    if (!feedContentDiv) {
        feedContentDiv = document.createElement('div');
        feedContentDiv.id = `feed-content-${feedId}`;
        document.getElementById('feed-content').appendChild(feedContentDiv);
        feedContentDiv.style.display = 'none';
    }

    if (feedContentDiv.style.display === 'none') {
        if (feedContentDiv.innerHTML.trim() === '') {
            const feedUrl = url;
            fetchAndRenderFeedContent(feedUrl, feedId);
        }
        document.getElementById('feed-content').style.display = 'block';
        feedContentDiv.style.display = 'block';
    } else {
        document.getElementById('feed-content').style.display = 'none';
        feedContentDiv.style.display = 'none';
    }
}

function fetchAndRenderFeedContent(feedUrl, feedId) {
    fetch(`/fetchFeedContent?url=${encodeURIComponent(feedUrl)}`)
        .then(response => {
            if (response.ok) {
                return response.text();
            } else {
                throw new Error('Failed to fetch feed content');
            }
        })
        .then(feedContent => {
            const feedContentDiv = document.getElementById(`feed-content-${feedId}`);
            if (feedContentDiv) {
                feedContentDiv.innerHTML = feedContent;
            }
        })
        .catch(error => {
            alert("Incorrect feed URL");
        });
}

function removeFeed(feedId) {
    fetch('/removeFeed', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: new URLSearchParams({ feedId: feedId })
    })
        .then(response => {
            if (response.ok) {
                fetchFeeds();
            } else {
                console.error('Failed to remove feed');
            }
        })
        .catch(error => {
            console.error('Error removing feed:', error);
        });
}