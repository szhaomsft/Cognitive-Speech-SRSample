const webpack = require('webpack');
const path = require('path');
const ProgressBarPlugin = require('progress-bar-webpack-plugin');

const sourcePath = path.join(__dirname, './src');
const staticsPath = path.join(__dirname, '../wwwroot/assets');

module.exports = function(env) {
  const nodeEnv = env && env.prod ? 'production' : 'development';
  const isProd = nodeEnv === 'production';

  const plugins = [
    new ProgressBarPlugin(),
    new webpack.EnvironmentPlugin({
      NODE_ENV: nodeEnv
    }),
    new webpack.NamedModulesPlugin()
  ];

  if (isProd) {
    plugins.push(
      new webpack.DefinePlugin({
        'process.env': {
          NODE_ENV: JSON.stringify('production')
        }
      }),
      new webpack.optimize.DedupePlugin(),
      new webpack.LoaderOptionsPlugin({
        minimize: true,
        debug: false
      }),
      new webpack.optimize.UglifyJsPlugin({
        output: {
          comments: false
        }
      }),
      new webpack.optimize.AggressiveMergingPlugin()
    );
  }

  return {
    devtool: isProd ? null : 'source-map',
    context: sourcePath,
    entry: {
      'app': './chatbot'
    },
    output: {
      path: staticsPath,
      filename: '[name].js',
      publicPath: '/assets/'
    },
    module: {
      rules: [
        {
          test: /\.jsx$/,
          exclude: /node_modules/,
          loader: 'babel-loader',
          query: {
            presets: [
              'es2015',
              'react',
              'stage-0'
            ],
            plugins: []
          }
        },
        {
          test: /\.js$/,
          exclude: /node_modules/,
          loader: 'babel-loader',
          query: {
            presets: [
              'es2015',
              'stage-0'
            ],
            plugins: []
          }
        },
        {
          test: /\.css$/,
          loader: 'style-loader!css-loader'
        },
        {
          test: /\.sass/,
          loader: 'style-loader!css-loader!sass-loader?outputStyle=expanded&indentedSyntax'
        },
        {
          test: /\.scss/,
          loader: 'style-loader!css-loader!sass-loader?outputStyle=expanded'
        },
        {
          test: /\.less/,
          loader: 'style-loader!css-loader!less-loader'
        },
        {
          test: /\.styl/,
          loader: 'style-loader!css-loader!stylus-loader'
        },
        {
          test: /\.(png|jpg|gif|eot|woff|woff2|ttf)$/,
          loader: 'url-loader?limit=8192'
        },
        {
          test: /\.json/,
          loader: 'json-loader'
        },
        {
          test: /\.(mp4|ogg|svg)$/,
          loader: 'file-loader'
        }
      ],
      noParse: [/moment.js/]
    },
    resolve: {
      extensions: ['.js', '.jsx'],
      modules: [
        path.resolve(__dirname, 'node_modules'),
        sourcePath
      ]
    },

    plugins,

    performance: isProd && {
        maxAssetSize: 100,
        maxEntrypointSize: 300,
        hints: 'warning'
    },

    stats: {
      colors: {
        green: '\u001b[32m'
      }
    }
  };
};
