const config = {
  getPath: () => {
    return window.$elmah_root || "/elmah";
  },
  getMaxErrors: () => {
    return window.$elmah_config && window.$elmah_config.maxErrors
      ? window.$elmah_config.maxErrors
      : 100;
  },
};
export default config;
