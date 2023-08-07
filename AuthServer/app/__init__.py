from flask import Flask, request, redirect
import requests

app = Flask(__name__)
redirect_uri = 'potato-vn://oauth-bgm'
client_secret = ''


@app.route('/bgm-oauth')
def bgm_oauth():
    code = request.args.get('code')
    if code is None:
        return "<h1>Code Not Found!</h1>", 400

    data = {
        'client_id': 'bgm26036422a855d5849',
        'client_secret': client_secret,
        'code': code,
        'grant_type': 'authorization_code',
        'redirect_uri': redirect_uri
    }
    header = {'Content-Type': 'application/json',
              'User-Agent': 'PotatoVN Auth Server'}
    response = requests.post('https://bgm.tv/oauth/access_token', json=data, headers=header)

    result = {}
    try:
        result = {
            'access_token':  response.json().get('access_token'),
            'refresh_token': response.json().get('refresh_token'),
            'expires': response.json().get('expires_in'),
        }
    except:
        return response.content, response.status_code

    return result, 200


@app.route('/bgm-refresh-token')
def bgm_refresh_token():
    token = request.args.get('refresh_token')
    if token is None:
        return "<h1>Refresh Token Not Found!</h1>", 400
    
    data = {
        'client_id': 'bgm26036422a855d5849',
        'client_secret': client_secret,
        'refresh_token': token,
        'grant_type': 'refresh_token',
        'redirect_uri': redirect_uri
    }
    header = {'Content-Type': 'application/json',
              'User-Agent': 'PotatoVN Auth Server'}
    response = requests.post('https://bgm.tv/oauth/access_token', json=data, headers=header)

    result = {}
    try:
        result = {
            'access_token':  response.json().get('access_token'),
            'refresh_token': response.json().get('refresh_token'),
            'expires': response.json().get('expires_in'),
        }
    except:
        return response.content, response.status_code

    return result, 200


@app.route('/')
def index():
    return "<h1>Welcome to PotatoVN auth server!</h1>"


if __name__ == "__main__":
    app.run()
