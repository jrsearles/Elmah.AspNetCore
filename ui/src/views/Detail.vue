<template>
  <div>
    <ErrorDetail :item="item" :id="id"></ErrorDetail>
  </div>
</template>

<script>
import axios from "axios";
import config from "../config";
import ErrorDetail from "@/components/ErrorDetail";
export default {
  name: "Detail",
  props: ["id"],
  data: function () {
    return {
      item: {},
    };
  },
  components: { ErrorDetail },
  mounted() {
    axios
      .get(config.getPath() + "/api/error?id=" + this.id)
      .then((response) => {
        this.item = response.data.error;
      })
      .catch((error) => {
        console.log(error);
        this.$bvToast.toast("Data loading error.", {
          variant: "danger",
          solid: true,
          noCloseButton: true,
          autoHideDelay: 2000,
        });
      });
  },
};
</script>

<style scoped></style>
